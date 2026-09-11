# Spec 2C follow-up — The cancellation that was reported as a failure — Design

**Date:** 2026-09-09
**Ticket:** [#130](https://github.com/karlssberg/Motiv/issues/130)
**Plan:** [`2026-09-09-spec-2c-followup-import-cancellation.md`](../plans/2026-09-09-spec-2c-followup-import-cancellation.md)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§6 — the EF Core reference store's migration path.
**Lineage:** Spec 2C ([#128](https://github.com/karlssberg/Motiv/pull/128)) → here.

## The defect

`StoreImport.CopyAsync` wraps a mid-import failure so that the caller is told the target is now
partially written and must be emptied before the import can ever succeed again. The catch filter
2C's final fix wave shipped was:

```csharp
catch (Exception exception) when (exception is not OutOfMemoryException
                                  && (propositionsWritten > 0 || ruleVersions > 0))
```

`Exception` includes `OperationCanceledException`, so a caller who cancelled mid-import received an
`InvalidOperationException` instead. Their `catch (OperationCanceledException)` — the one a graceful
shutdown is built on — never fired, and the shutdown they asked for was handled as an unexpected
error.

**No live consumer was affected.** The sample passes `CancellationToken.None`
(`src/examples/Motiv.RulesEngine.Sample/Program.cs`), and the wrapped message was not *false*: a
cancelled import really has left a partial target. Only a library consumer who cancels and catches
cancellation specifically could see it. The ticket was raised by the whole-branch review of #128 and
deferred as non-blocking on exactly that basis.

### It is the outlier, not a new rule

The filter's `is not OutOfMemoryException` clause shows the author already knew some exceptions must
not be dressed up as something else. It caught the exception nobody can meaningfully wrap and missed
the one that is not a failure report at all. The repository had already settled the question three
times over, in the same idiom, before 2C was written:

| Site | Filter |
|---|---|
| `Motiv.Serialization/Decisions/DecisionLog.cs:233,245` | `when (exception is not OperationCanceledException)` |
| `Motiv.Serialization/Governance/ChangeRequestSet.cs:1166` | `when (exception is not OperationCanceledException)` |
| `Motiv.Serialization.Sql/SqlDecisionSink.cs:309` | `when (exception is not OperationCanceledException)` |

So the fix is consistency with a convention the codebase already holds, not a new position being
taken. That is also why the guard stays **type-based** rather than checking
`cancellationToken.IsCancellationRequested`: every other site in the codebase tests the type, and a
store that cancels on a *linked* token — a request abort, an internal timeout — is still cancelling,
still not reporting a failure, and still owes its caller a cancellation exception.

## Decisions (locked)

### 1. Cancellation propagates as *itself*, not as a reconstruction of itself

The obvious way to keep both properties — a cancellation type *and* the partial-state sentence —
is to rethrow a fresh `OperationCanceledException(message, exception, cancellationToken)`. It is
wrong, and wrong in a way this ticket should be the last place to get wrong:
`TaskCanceledException` derives from `OperationCanceledException`, so rebuilding the exception
flattens the derived type away. That is *the same defect as #130, one level down* — a caller whose
`catch (TaskCanceledException)` used to fire would stop seeing it, in the commit whose entire purpose
is that a caller's catch keeps firing.

The original instance is therefore rethrown untouched: same type, same `CancellationToken`, same
stack, same identity. `Should_keep_the_derived_cancellation_type_a_caller_may_be_catching` asserts
`ShouldBeSameAs`, not `ShouldBeOfType`, so a future rewrap cannot pass it by reconstructing an
equivalent exception.

### 2. The partial-state warning survives, on `Exception.Data`

The ticket asks that the warning not be dropped: "consider logging it on the cancellation path
rather than silently rethrowing." There is no seam to log through. `StoreImport` is a `static` class
with no dependency injection, and the rules-stack telemetry it might otherwise use
(`MotivRulesTelemetry.ActivitySource`) is `internal` to `Motiv.Serialization`, whose
`InternalsVisibleTo` list names only `.Tests` and `.AspNetCore` — not this package. Taking a logger
parameter would widen a public signature for a bug fix, and would put the burden on every caller
including the ones who never cancel.

`Exception.Data` is the only channel that carries the sentence while leaving decision 1 intact: it
mutates a dictionary that is already part of the exception, and touches neither type nor token nor
stack. Its one weakness is discoverability, so the key is published as
`StoreImport.PartialImportDataKey` and named in the method's own `<param>` documentation. A key
nobody can find is the "silently rethrowing" the ticket objects to, wearing a dictionary.

The description is the *same sentence* the failure path puts in its message, so a consumer reading
either channel is told the same thing about the same state.

### 3. It is marked only when something was actually written

The marker is applied under the same `propositionsWritten > 0 || ruleVersions > 0` condition that
gates the wrapper. A cancellation before the first write leaves the target empty and a retry clean;
marking that one as a partial import would send an operator to drop and recreate tables that hold
nothing. `Should_not_describe_a_partial_state_on_a_cancellation_that_wrote_nothing` holds this, and
is the one new test that passes against the *unfixed* code — it exists to stop the marker being
over-applied, not to prove the fix.

### 4. Attaching the description must never cost the delivery

`Exception.Data` is a *virtual* property. A consumer's own `OperationCanceledException` subclass may
return `null` from it, or a dictionary that refuses writes — at which point an unguarded
`Data[key] = …` throws from inside the catch and replaces the caller's cancellation with a
`NotSupportedException`. That is #130 again in a different exception type, produced by the fix for
#130.

So the write is guarded by `if (exception.Data is { IsReadOnly: false } data)` — a pattern rather
than a `try`/`catch`, because it covers the null case in the same expression and swallows nothing.
Describing the partial state is a courtesy; delivering the caller's cancellation is the contract, and
the courtesy never costs the contract.

`Should_still_let_the_cancellation_out_when_its_Data_refuses_the_description` holds it, against a
cancellation type whose `Data` is a read-only dictionary. It was **red before the guard**, so this is
a real failure mode rather than a defensive gesture.

### 5. The tests capture with `Record.ExceptionAsync`, not `ShouldThrowAsync`

This is not a style preference, and it is the finding worth carrying out of this slice.

An `async` method that throws an `OperationCanceledException` does **not** return a faulted task.
`AsyncTaskMethodBuilder.SetException` special-cases cancellation into `TrySetCanceled`, so the task
enters the *Canceled* state. xUnit's `Record.ExceptionAsync` rethrows the stored instance; Shouldly's
`ShouldThrowAsync` hands back a **freshly constructed `TaskCanceledException`** instead.

Measured directly, on the same helper, differing only in the capture:

| Captured with | Same instance | Type seen | `Data` seen |
|---|---|---|---|
| `Record.ExceptionAsync` | yes | `OperationCanceledException` / `TaskCanceledException` | present |
| `ShouldThrowAsync` | **no** | `TaskCanceledException` for both | **lost** |

Every property decisions 1 and 2 exist to guarantee — the identity, the derived type, the `Data`
entry — is destroyed *by the assertion helper* before the assertion runs. A test suite written
through `ShouldThrowAsync` could never observe the thing it was written to prove, and the two tests
that pin decisions 1 and 2 would have failed against correct code. That is worse than the original
defect: it is a test that cannot see its own subject.

The rest of the file stays on Shouldly for the assertions themselves; only the *capture* changes.
The reason is written into the test comment as well as here, because the natural instinct of the
next person editing this file is to make it consistent with its neighbours.

## What is unchanged

- The wrapper on the ordinary failure path and its `OutOfMemoryException` exclusion. Its message
  keeps every phrase any test or doc relies on; only the opening verb moves from "failed" to
  "stopped", so that one sentence can serve a cancellation as honestly as a failure.
- Every existing `StoreImportTests` case, including the two that pin both operands of the
  target-was-mutated guard.
- The propositions-before-rules ordering and the refuse-a-non-empty-target contract.
- `StoreImportResult` and the `CopyAsync` signature. `PartialImportDataKey` is the whole of the added
  public surface.

## Verification

`Motiv.Serialization.EntityFrameworkCore.Tests` — the five new cases plus the nine existing
`StoreImportTests` — and the full solution suite. See the plan for what was run and what could not
be.
