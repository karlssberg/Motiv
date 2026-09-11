# Spec 2C follow-up — The cancellation that was reported as a failure — Implementation Plan

**Design:** [`2026-09-09-spec-2c-followup-import-cancellation-design.md`](../specs/2026-09-09-spec-2c-followup-import-cancellation-design.md)
**Ticket:** [#130](https://github.com/karlssberg/Motiv/issues/130)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§6 — the EF Core reference store's migration path, shipped as Spec 2C
([#128](https://github.com/karlssberg/Motiv/pull/128)).

## What this is

`StoreImport.CopyAsync`'s partial-import catch filter also caught `OperationCanceledException` and
rewrapped it as `InvalidOperationException`, so a caller who cancelled lost the cancellation type.
The fix excludes cancellation from the wrap and lets the original instance out untouched, while
keeping the partial-state warning on `Exception.Data` under a published key.

Scope is one file of production code, one test file, and this pair of docs.

## Global constraints

- **The exception instance is rethrown, never rebuilt.** `TaskCanceledException` derives from
  `OperationCanceledException`; reconstructing the exception to attach a message would flatten it
  away, which is #130's own defect one level down.
- **No new dependency, no widened signature.** `StoreImport` is static with no DI, and the rules
  telemetry it might log through is `internal` to a package this one cannot see. The only public
  surface added is one `const string`.
- **The failure path does not change.** Its `OutOfMemoryException` exclusion and all nine existing
  `StoreImportTests` cases stay exactly as they are; its message keeps every phrase those tests
  rely on.

## Sequence

1. **Read the guard's existing coverage.** Two of the nine existing tests pin the two operands of
   `propositionsWritten > 0 || ruleVersions > 0` separately, and say so in their comments. The new
   tests reuse the `propositionsWritten > 0` arrangement, so they exercise the guard at its true
   branch rather than dodging it.
2. **Write four failing tests** in `StoreImportTests.cs`, generalising `FailingRuleStore` with a
   `Func<Exception> Failure` so a test can inject a cancellation where it previously injected only
   `ImportFailure`:
   - the exception's *type* survives (`OperationCanceledException`, token preserved);
   - the *derived* type survives, asserted with `ShouldBeSameAs` so a rewrap cannot pass;
   - the partial-state sentence is on `Data[StoreImport.PartialImportDataKey]`;
   - a cancellation that wrote nothing carries **no** marker.
3. **Watch them fail for the right reason.** The first run is a compile error only
   (`PartialImportDataKey` does not exist), which is not yet evidence of the behavioural defect — so
   add the constant alone and run again to see the three genuine reds. Read `error CS` out of the
   output explicitly: a filtered `dotnet test` exits 0 on a compile failure and looks like a pass.
4. **Fix the filter**: `&& exception is not OperationCanceledException`, with the partial-state
   description written to `Data` on the way past, under the same
   `propositionsWritten > 0 || ruleVersions > 0` condition that gates the wrapper. Factor the
   sentence so both paths carry the identical text rather than two drifting copies.
5. **Update the XML docs** — `<param name="cancellationToken">` and the `<exception>` list — so the
   contract states which exception a cancelled import produces and where the description is found.
6. **Full solution suite**, per `CLAUDE.md`: the example projects assert on message text, so a
   changed sentence would surface there rather than in `Motiv.Tests`.
7. **`code-simplifier` pass**, then re-run the affected suite — and re-establish the red, because a
   pass that rewrites the assertions invalidates the red observed through the old ones.

## Expected fallout

None outside `StoreImport.cs`. The behaviour change is only reachable by a caller who cancels
mid-import, and the sample passes `CancellationToken.None`.

## What the execution actually turned up

Both surprises were in the *tests*, and both would have produced a green suite that proved nothing.
They are recorded here because neither is visible in the final diff.

1. **The first draft of the cancellation tests cancelled the token before calling `CopyAsync`.**
   `CopyAsync`'s first act is `await targetRules.LoadAsync(cancellationToken)`, so an
   already-cancelled token throws *outside the try block* — and the test that exists to prove the
   catch filter no longer swallows cancellation would have passed against the unfixed code, having
   never reached the filter. The store now requests cancellation from inside the failing append,
   which is also what really happens: the token trips while a write is in flight.

2. **Shouldly's `ShouldThrowAsync` cannot see what these tests assert.** See decision 4 of the
   design doc — it reconstructs the exception for a task in the Canceled state. This surfaced as
   two tests that stayed red *after* a correct fix, which is the failure mode worth naming: the
   instinct at that point is to change the production code until the assertion goes green, and doing
   so here would have meant reconstructing the exception — the exact defect the ticket exists to
   fix. It was settled by a throwaway probe test measuring both capture paths against the same
   helper, not by reasoning.

The second cost a full debug cycle, and the lesson generalises past cancellation: **when a test
disagrees with code you have reason to believe is correct, measure the harness before editing the
subject.**

3. **`Exception.Data` can refuse the write.** Raised independently by the review pass and by the
   implementation; see decision 4 of the design doc. Worth recording that it was *red before the
   guard* — the failure mode is real, and the guard is not a defensive gesture.

## Verification obligations

- `dotnet test` over the whole solution, exit code read rather than inferred from `Passed!` lines —
  the first full run reported thirteen `Passed!` suites and still exited 1.
- The four fix-proving cases red before the fix and green after, **re-verified twice**: once after
  the assertion helper changed, and again after the `code-simplifier` pass restructured the tests
  into a shared helper. A red observed through assertions that were later rewritten is not evidence
  about the ones that shipped. The fifth (the over-application guard) is green either way by
  construction.
- The nine existing `StoreImportTests` cases unchanged and green.

## What was run

| Suite | Result |
|---|---|
| Whole solution via `~/.dotnet/dotnet test Motiv.slnx` | 17 suites, all green |
| `Motiv.Tests` (net10.0, net8.0, net9.0) | 6022 × 3 green |
| `Motiv.Serialization.Tests` (net10.0, net8.0, net9.0) | 996 × 3 green |
| `Motiv.Serialization.EntityFrameworkCore.Tests` | 48 green (14 of them `StoreImportTests`) |
| Example projects — Poker, ECommerce, SmartHome | green |
| `dotnet build Motiv.slnx` including net472 | clean, no warnings |

**Not run, and why.** The `net472` targets of `Motiv.Tests` and `Motiv.Serialization.Tests` abort on
this host — vstest needs a `mono` host that macOS does not have. They *build* clean, which is what
this change could plausibly break, and CI runs them. `Motiv.Studio.Tests` aborted once inside the
full run on a 90-second test-host protocol timeout under load, and is green (109) when run on its
own. The `ui/` workspace was not run: nothing in this slice touches it.

The system SDK muxer runs only net10.0; the net8/net9 legs need `~/.dotnet/dotnet`, and a run
through the system muxer reports them as launch failures rather than as skips.
