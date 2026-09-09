# Spec 3E follow-up — The bound that stopped at the synchronous folds — Design

**Date:** 2026-09-07
**Ticket:** [#204](https://github.com/karlssberg/Motiv/issues/204)
**Plan:** [`2026-09-07-spec-3e-followup-async-evaluation-budget.md`](../plans/2026-09-07-spec-3e-followup-async-evaluation-budget.md)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) and §7.
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#145](https://github.com/karlssberg/Motiv/issues/145) (the measurement) →
[#202](https://github.com/karlssberg/Motiv/issues/202) (per-fold → per-evaluation, synchronous) → this.

## What the defect was

#202 fixed the count and left the carrier's reach unexamined. `EvaluationBudget` is a
`[ThreadStatic] int` that `EvaluationFold.Fold` claims at the outermost entry and every nested fold
inherits — so a decorator between two operator layers spends its caller's budget rather than starting a
fresh one. `AsyncEvaluationFold.FoldAsync` still held `var size = 1` in a local, so the asynchronous
pair admitted what the synchronous pair refused: fifty decorator layers of ten operands, over a thousand
nodes, under a limit of a hundred.

The shape is what a rule document composes. `RuleBinder.Decorate` wraps every node carrying a `name` or
a `whenTrue`, and `AsyncRuleBinder` binds those same documents asynchronously.

## Why it needed a second change rather than a second line

The obvious repair — have the asynchronous fold call `EvaluationBudget.Enter()` — is not merely
insufficient, it is **unsafe**, and the reason is worth stating precisely because it is the opposite of
the usual thread-static complaint.

A thread-static is normally wrong across `await` because the value is *stale* — the thread has moved on
and you read something left behind. Here it is wrong because the value is **live**. A continuation may
resume on a pool thread whose slot holds a *suspended* evaluation's count. Both evaluations are running;
neither is finished; and every charge either makes lands on the other's total. The corruption runs in
both directions — an undercount weakens the bound, an overcount refuses a composition that is within it
— and the second is the worse one, because it turns a size limit into a source of spurious failures
whose cause is another request.

So the carrier had to become a **flow**, not a thread: `AsyncLocal<Counter>`, where `Counter` is a
mutable box. The box matters. `AsyncLocal<T>` *writes* copy the `ExecutionContext`; a write per node
would be unaffordable, a write per evaluation is not — it is the same trade Spec 3E made when it gave
the asynchronous fold a fresh frame array rather than a cached one, next to a state machine per awaited
operand.

## The release nobody wrote

The scope has to end when the evaluation does, or the next caller inherits its spending. On the
synchronous side that is `Ownership`, which zeroes the count when the fold that owned it leaves. On the
asynchronous side there is no equivalent gesture available: a `Dispose` that runs after an `await`
executes on the resumed execution context, and writes there do not reach the caller.

They do not have to. `AsyncTaskMethodBuilder.Start` captures the thread's `ExecutionContext` before the
first `MoveNext` and restores it after that call returns. So an `AsyncLocal` write made in an async
method's **synchronous prefix** is:

- **visible** to everything that method goes on to await, because the continuation captures the context
  the write produced; and
- **invisible** to the method's caller on return, because the builder puts the caller's context back.

That is exactly the scope wanted, and it is why `EnterAsync()` is called before the fold's first
`await` rather than wrapped around it. The release is the runtime's rather than ours.

**This is a runtime detail load-bearing for a correctness property, so it is pinned by behaviour rather
than trusted.** `AsyncEvaluationBudgetTests.Should_start_each_async_evaluation_with_its_whole_budget`
runs the same composition three times at a limit equal to its exact size; if the context leaked, the
second call would fail at half the composition. `Ownership.Dispose` also nulls the slot, which is inert
if the builder has already restored it and a repair if some runtime has not.

## Three shapes the synchronous surface does not have

### 1. A concurrent operator is not a fold

`AsyncAndSpec`, `AsyncOrSpec` and `AsyncXOrSpec` take a `concurrent` flag and evaluate both operands
through `Task.WhenAll`. The fold leaves such a node to evaluate itself, so **nothing above the two
branches holds a budget for them to inherit**. Each branch enters a fold, each finds no counter, each
becomes its own root — and the bound applies per branch. That is the per-fold defect in a second place,
and no amount of fixing `FoldAsync` reaches it.

The fan-out is therefore a second place an evaluation can *begin*, and it gets its own entry point. It
differs from the fold's in exactly one respect, and the difference is **which node is being entered**:

- a fold's root is the node *below* whatever reached it — a decorator's inner spec, which the fold above
  did not charge, so `EnterAsync` charges it unconditionally;
- a fan-out's root is the concurrent node *itself*, which the fold above already charged as an operand
  — so `EnterFanOut` charges it only when nothing reached it, which is to say when the concurrent node
  is the whole evaluation.

Both directions are held by a case, and both were mutation-proved (below). `ExecutionContext` flows a
copy of the `AsyncLocal` *value* into each branch, and the value is a reference, so both reach the same
box: the charge is `Interlocked.Increment`, checked on every increment rather than sampled. A fan-out
can pass the bound in two branches at once; both are then refused and `Task.WhenAll` surfaces one, which
is the right outcome — the count only rises within an evaluation, so which branch reports it is not
information a caller can use.

### 2. A synchronous composition inside an asynchronous one

This is the decision the ticket was most explicit about not making: *"the asymmetry does not disappear
when the asynchronous side is fixed; it moves."* An asynchronous composition reaches a synchronous one
through `SyncSpecAsyncAdapter` — every `ToAsyncSpec()` — which runs a synchronous fold on the current
thread.

The two options were one carrier, or a documented exclusion at the seam. **One carrier**, because the
alternative reinstates a per-fold bound in a new place: a caller could spend twice the bound by putting
half the composition behind an adapter. So `Enter()` resolves the flowed counter first and charges it
when one is in force; the thread-static is what it falls back to, not what it prefers.

The cost is one `AsyncLocal` read per **fold entry** — not per node. Both entry points hand back an
`Ownership` that already holds the counter it resolved, so the per-node charge is a predicted branch on
a field the struct is carrying. `EvaluationBudgetTests.Should_carry_the_budget_without_allocating` still
holds at 0 bytes, which is what keeps `Matches`' allocation-free contract intact.

### 3. An exclusion inside a shared counter

`Exclude()` parks the composition's count so that work done *inside* a node — higher-order element
resolution, `EnumerableExtensions.Where`, a `Tap` callback,
[#209](https://github.com/karlssberg/Motiv/issues/209)'s telemetry — is bounded afresh and costs the
composition nothing.

Parking a *flowed* counter cannot be done the way parking the thread-static is. Zeroing a box that a
concurrent operator's other branch is still charging would discard that branch's spending when the
exclusion restores it. So the flowed counter is **detached rather than zeroed**: the slot is hidden for
the duration, the excluded work falls through to the thread-static and is bounded on its own account,
and the counter itself is never touched, so the other branch keeps counting correctly.

That the two writes can be restored on the same thread is not an assumption — `Exclusion` is a
`ref struct`, so the compiler refuses a scope that spans an `await`, and a scope that cannot suspend
cannot resume elsewhere. The restriction was already there for other reasons; this is the first thing
that depends on it.

## What the review of the tests found

Four claims in this change are not visible to the arithmetic cases, and each was mutation-proved by
breaking the code and watching exactly one case go red.

| Mutation | Caught by | Out of |
|---|---|---|
| carrier changed from `AsyncLocal<Counter>` to `[ThreadStatic] Counter` | `Should_not_charge_a_suspended_evaluation_to_one_running_on_the_same_thread` | 17 |
| `Exclude()` stops detaching the flowed counter | `Should_not_charge_a_tap_callbacks_own_evaluation_to_an_async_budget` | 6,003 |
| `EnterFanOut` charges when nested | `Should_not_charge_a_nested_concurrent_node_twice` | 12 |
| `EnterFanOut` never charges its root | `Should_charge_a_concurrent_node_that_is_the_whole_evaluation` | 12 |

The first is the one worth reading. **Nothing else in the suite can see the difference between a
fold-local count, a thread-static and a flowed one** — all three bound a single unsuspended evaluation
identically, and every arithmetic case here is a single unsuspended evaluation. The case that separates
them has to arrange an interleaving, and it does so by *choosing* one rather than racing for it: a gated
leaf suspends the first evaluation having charged exactly two nodes, and the second evaluation is built
entirely from synchronously-completing operands so it runs to completion on the thread the first one
left. The limit admits either alone and refuses their sum.

This is the same discipline as the telemetry gauge and the leak canary before it: **a test written
against the design rather than against the defect is not red on arrival, so it has to be made red on
purpose or it is decoration.** It passed against the code as it stood before this change, and it passes
now; the only thing that establishes it as a guard is that it fails against the carrier the ticket
rejected.

The second is worth reading for the ratio. One case out of 6,003 sees the exclusion's flowed half —
because it is the only case in the suite that puts an exclusion *inside* an asynchronous evaluation, and
`ToAsyncSpec()` is what puts it there. Every other exclusion case in `EvaluationBudgetTests` and
`TelemetryBudgetTests` runs on the synchronous carrier, where the mutation is invisible.

## What is now true, and where it is said

`MotivLimits.MaxEvaluationSize`'s remarks and `docs/limits/index.md` both carried the asymmetry as a
live caveat — the docs as a `[!NOTE]` advising hosts to *"refuse the document at the edge if you
evaluate untrusted compositions asynchronously."* Both now state the bound rather than the hole, and
both name the two shapes that were not obvious: a concurrent operator's branches count against one
budget, and a synchronous proposition reached through `ToAsyncSpec()` counts against the asynchronous
evaluation containing it.

They move together because they are the only statement of the line a caller can read, which is the rule
#213 restates for the case that is still open.

## What was deliberately not done

- **The `IOperationFold` → `IFoldableOperation` rename,
  [#211](https://github.com/karlssberg/Motiv/issues/211).** It asks to be folded into whichever
  budget/traversal slice is next open, and this is that slice. It is left open anyway: it touches 21
  files with 104 references and reviews as pure noise, and putting it in the same diff as a change to
  what the budget *means* would make the semantics harder to check for no gain the ticket cannot get
  later. Its own argument — that the churn is cheap while these files are open — is about the cost to
  the author, and the cost that matters here is to the reviewer.

  > **Landed since.** The rename shipped standalone after all
  > ([#211](https://github.com/karlssberg/Motiv/issues/211)) — every slice it could have ridden along
  > with declined it for the reviewer-cost reason recorded here, and there were no candidates left.

- **The asynchronous fold's depth.** #145's residual 2 and
  [#201](https://github.com/karlssberg/Motiv/issues/201) are about how deep a composition may nest;
  this is about how large it may be. The concurrent operators still recurse, and this change does not
  alter that — it only makes their two branches share a size budget.
- **A per-fold fast path for applications that never evaluate asynchronously.** `Enter()` and
  `Exclude()` both read the `AsyncLocal` slot unconditionally, and a process-wide "no asynchronous
  evaluation has ever run" flag would make both free for a purely synchronous host. It is sound —
  the flag is only ever set, and set before any counter can exist — but it is an optimisation without a
  measurement behind it, and this series has been clear that a number nobody has paid is not a number.
  Worth filing if the read shows up.

## Files

| File | Change |
|---|---|
| `src/Motiv/Traversal/EvaluationBudget.cs` | Second carrier; three entry points; the charge moves onto `Ownership`; `Exclude` detaches the flowed counter |
| `src/Motiv/Traversal/AsyncEvaluationFold.cs` | `var size` → `EvaluationBudget.EnterAsync()`, claimed in the synchronous prefix |
| `src/Motiv/Traversal/AsyncConcurrentFanOut.cs` | New. Six identical fan-out blocks become two, and the budget's fan-out entry lives here |
| `src/Motiv/Traversal/EvaluationFold.cs` | Charges through the handle |
| `src/Motiv/{And,Or,XOr}/Async*Spec.cs` | Concurrent branches route through the fan-out |
| `src/Motiv/MotivLimits.cs`, `docs/limits/index.md` | The asymmetry caveat replaced by what is now true |
| `src/Motiv.Tests/Traversal/AsyncEvaluationBudgetTests.cs` | New. Twelve cases |
| `src/Motiv.Tests/Traversal/DecoratorSeamTests.cs` | The characterisation case flipped, plus its `MatchesAsync` twin |

## Verification

`Motiv.Tests` 6,003 pass on net8.0, net9.0 and net10.0, and the whole solution is green — the example
projects, `Motiv.Serialization` and its four satellites, `Motiv.Studio`, the analyzer and the code-fix
suites. `Motiv`'s `netstandard2.0` target builds, which is the one that matters for `AsyncLocal<T>`
availability.

**net472 could not be run or built here** — the .NET Framework targeting pack is not installed on this
machine, so `dotnet build -f net472` fails with MSB3644. `Motiv.Tests` targets it and CI builds it, so
that leg is unverified locally. It is the leg where the `AsyncTaskMethodBuilder.Start` restore above
would be worth a second look if anything goes red.
