# Spec 3E follow-up — The bound that stopped at the synchronous folds — Plan

**Date:** 2026-09-07
**Ticket:** [#204](https://github.com/karlssberg/Motiv/issues/204)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) — *"a **result-size bound counted in the traversal loop** replaces the crash
that used to cap the amplification finding"* — and §7, *"a 100k-deep composition no longer crashes and
is bounded by the result-size limit."*

## Why this slice exists

[#202](https://github.com/karlssberg/Motiv/issues/202) made `MotivLimits.MaxEvaluationSize` bound one
*evaluation* rather than one *fold*, which is what it had always documented. It did so on the
**synchronous** folds only. `AsyncEvaluationFold.FoldAsync` kept `var size = 1` in a local, so:

| Composition | Nodes | `MaxEvaluationSize = 100` |
|---|---|---|
| flat `And` chain of 200, sync or async | 399 | refused |
| 50 decorator layers × 10 operands, `Evaluate` / `Matches` | > 1,000 | refused (since #202) |
| the same, `EvaluateAsync` / `MatchesAsync` | > 1,000 | **accepted** |

`DecoratorSeamTests.Should_not_yet_bound_a_decorator_layered_async_evaluation` has been pinning that
last row as behaviour, with an instruction in its own remarks to flip it when this lands.

It is reachable from a rule document. `RuleBinder.Decorate` wraps every node carrying a `name` or a
`whenTrue`, so a document composes the alternating shape by construction, and `AsyncRuleBinder` binds
the same documents to the asynchronous surface. A host that evaluates untrusted documents
asynchronously has the bound the docs promise on one entry point and not the other.

## The three decisions the ticket left open

#204 filed the defect rather than fixing it, and named what had to be settled first.

### 1. The carrier

`EvaluationBudget` is a `[ThreadStatic] int`. That is not merely *unavailable* to an asynchronous
evaluation — it is **wrong** for one. A continuation may resume on a thread whose slot holds a
*suspended* evaluation's count. Not a stale count; a live one. Two interleaved evaluations would
corrupt each other in both directions: an undercount weakens the bound, an overcount refuses a
composition that is within it.

The ticket proposes `AsyncLocal<T>` over a mutable box — a write per node through `AsyncLocal` would
copy the `ExecutionContext` per node, which is not affordable; a write per *evaluation* is, next to the
state machine an asynchronous evaluation already allocates per awaited operand.

**Decision: `AsyncLocal<Counter>`, written once per evaluation, mutated thereafter.** The thread-static
stays for the synchronous folds, where it is correct and free — and where `Matches` has an
allocation-free contract Spec 3E paid for with a per-thread frame buffer, which this must not spend.

### 2. What a concurrent operator's fan-out does to one counter

`AsyncAndSpec`, `AsyncOrSpec` and `AsyncXOrSpec` take a `concurrent` flag and evaluate both operands
through `Task.WhenAll`. `ExecutionContext` flows into both branches, so both reach the same box.

**Decision: `Interlocked.Increment`, checked on every increment rather than sampled.** A fan-out can
pass the bound in two branches at once; both are then refused and `Task.WhenAll` surfaces one. The
count only rises within an evaluation, so once it is past the bound every later charge is too, and
which branch reports it is not information a caller can use.

There is a second half the ticket does not mention: **a concurrent node is not folded at all**, so
nothing above the two branches holds a budget for them to inherit. The fan-out is therefore a second
place an evaluation can *begin*, and needs an entry point of its own.

### 3. Where the flowed budget meets the thread-static one

An asynchronous composition reaches a synchronous one through `SyncSpecAsyncAdapter` — every
`ToAsyncSpec()` — whose evaluation runs a synchronous fold. That fold reads the thread-static, finds
nothing, and starts a fresh budget. The ticket puts it plainly: *"the asymmetry does not disappear when
the asynchronous side is fixed; it moves."* Either both folds read one carrier, or the seam is
documented as another exclusion.

**Decision: one carrier.** The synchronous fold resolves the flowed counter first and charges it when
one is in force. Documenting a new exclusion here would reinstate a per-fold bound in a new place,
which is the exact failure this series exists to stop: a caller could spend twice the bound by putting
half a composition behind `ToAsyncSpec()`.

## Approach

TDD, red first.

1. **Flip the characterisation case** in `DecoratorSeamTests`, and add its `MatchesAsync` twin.
2. **New `AsyncEvaluationBudgetTests`**, matching `EvaluationBudgetTests`' idiom — exact arithmetic
   rather than comfortable margins, and an admit/refuse pair around each claim:
   - the decorator-layered arithmetic, node for node with its synchronous twin;
   - a concurrent operator's two branches against one budget, plus the two cases that pin *which* node
     the fan-out charges;
   - a synchronous sub-composition charged to the asynchronous evaluation containing it;
   - a suspended evaluation not charged to one running on the thread it left;
   - release on both the normal and the abandoned path;
   - the exclusion end: a `Tap` callback inside an asynchronous evaluation still costs nothing.
3. **Implement**: a second carrier on `EvaluationBudget`; `Enter` resolves the flowed counter first;
   `EnterAsync` and `EnterFanOut` establish one; the per-node charge moves onto the handle so the
   resolution is per fold rather than per node.
4. **Extract the fan-out.** Six structurally identical blocks across three operators become
   `AsyncConcurrentFanOut`, which is where the budget's fan-out entry has to live anyway.
5. **Mutation-prove the guards.** The interleaving case in particular is not red against the code as it
   stands — a fold-local count is per-fold and passes it. It has to be proved against the *thread-static*
   carrier the ticket rejects, or it is decoration.
6. **Update what states the contract**: `MotivLimits.MaxEvaluationSize` remarks and
   `docs/limits/index.md`, which both currently carry the asymmetry as a live caveat.

## Expected fallout

- **`Matches`' allocation-free contract.** `Enter()` gains an `AsyncLocal` read per fold entry. It
  allocates nothing, but `EvaluationBudgetTests.Should_carry_the_budget_without_allocating` holds 0
  bytes and is the case to watch.
- **A `ToAsyncSpec()` adapter now costs a node against the evaluation containing it.** That is not new
  behaviour so much as newly visible: it is a decorator, and decorators have been charged since #202.
- **`Ownership` can no longer be a `ref struct`** — the asynchronous fold's scope spans its `await`s, so
  it lives in a state-machine field. `Exclusion` keeps the restriction and relies on it.

## Out of scope

- The asynchronous fold's **depth**. #145's residual 2 (the concurrent operators still recurse) and
  [#201](https://github.com/karlssberg/Motiv/issues/201) (the alternating ceiling) are about depth; this
  is about size.
- The `IOperationFold` → `IFoldableOperation` rename,
  [#211](https://github.com/karlssberg/Motiv/issues/211). It recommends being folded into whichever
  budget slice is next open, and this is that slice — but it is a 30-file rename that would swamp a
  semantics change in review. Left open deliberately, with the reason recorded in the design doc.

  > **Landed since.** The rename shipped standalone after all
  > ([#211](https://github.com/karlssberg/Motiv/issues/211)) — every slice it could have ridden along
  > with declined it for the reviewer-cost reason recorded here, and there were no candidates left.
