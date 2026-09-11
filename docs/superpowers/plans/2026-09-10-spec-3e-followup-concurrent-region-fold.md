# Plan — the fan-out that was folded by nesting it

Source: bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 *Structural safety (19)*, §4 (*"no uncatchable crash, bounded work"*), §7.
Ticket [#145](https://github.com/karlssberg/Motiv/issues/145), residual 2 of
[Spec 3E](https://github.com/karlssberg/Motiv/pull/144).

## The defect

Spec 3E folded the logical operators onto the heap and left two shapes recursive. Residual 1 —
decorator nesting — was measured and decided in [#203](https://github.com/karlssberg/Motiv/pull/203),
which re-scoped this ticket to residual 2.

`AsyncAndSpec`, `AsyncOrSpec` and `AsyncXOrSpec` take a `concurrent` flag that evaluates both operands
through `Task.WhenAll`. That is a fan-out rather than a walk, so `IAsyncFoldableOperation.IsConcurrent`
told the driver to skip such a node and leave it to evaluate itself — and a nest of them therefore
recursed once per layer, over `AsyncConcurrentFanOut`'s state machine plus each operand's own fold
entry. #145 measured the ceiling at **669** layers on a 1 MB thread (`EvaluateAsync`) and 1,037 on
`MatchesAsync`; re-measured out of process for this slice on .NET 9 Release it is between **400 and
450**, which is the other reason not to ship a number.

The ticket asked for "a genuinely parallel fold — a different algorithm, not a variation on the
existing one."

## The shape of the fix

It is a variation on the existing one, because of what a nest already means.
`a.AndConcurrently(b.AndConcurrently(c))` starts `a` and the inner node together, and the inner node
starts `b` and `c` the instant it is reached — so all three are in flight either way. The nesting was
never the source of the concurrency, only how it was spelled.

So the driver absorbs an unbroken run of concurrent operations into one **region**, starts every
operand at that region's boundary in a single `Task.WhenAll`, and composes the region from the answers
afterwards. The concurrency is preserved exactly rather than approximated; what is dropped is the
`Task.WhenAll` per layer and the stack frame that came with it.

What makes the region walkable without evaluating anything is that concurrency implies **eagerness**:
`NextOperand` returns the same operand whichever outcome it is told, so the region's shape is a
property of the composition rather than of the run. That is the one thing a frame of the existing
machine cannot assume, and the whole reason the two need different loops.

## Steps

- [x] Failing evidence, out of process (a stack overflow aborts the runner rather than failing a
      test): a 20,000-layer `AndConcurrently` nest on a 1 MB thread exits 134, and the last depth that
      returns is between 400 and 450.
- [x] `AsyncEvaluationFold.FoldConcurrentlyAsync` — the region walk, the single fan-out, the backwards
      composing pass.
- [x] `FoldAsync` dispatches to it, both for a concurrent root and for a concurrent operand.
- [x] Delete `AsyncConcurrentFanOut` and `EvaluationBudget.EnterFanOut`; collapse the `if (!concurrent)`
      branch out of all six entry points on the three operators.
- [x] Charge the region's nodes — nothing walked them before, so nothing charged them, and a nest of
      any depth cost the budget one node. Invisible while the stack capped the depth; not now.
- [x] Tests: deep concurrent chains for all three operators and both entry points at 50,000 operands;
      nest parity against the sequential twin; a three-operand nest that deadlocks unless all three
      start at once; the two budget cases.
- [x] `DecoratorNestingTests` — the `Concurrent(160)` case retires and its table rows are marked
      removed; the shape's cover moves to `DeepEvaluationTests`.
- [x] `docs/limits/index.md` and `docs/async/ConcurrentOperators.md`.
- [x] `code-simplifier` pass; full solution suite.

## Not in scope

**Alternating concurrent and sequential operations.** A sequential operation at a region's boundary is
handed to a fold of its own, so a composition that alternates the two still costs a frame per
alternation — bisected out of process, that shape now returns to **298** layers on a 1 MB thread.
Removing it means one loop driving several live cursors at once, with a completion queue and a signal,
which is a genuinely different algorithm; and no rule document can compose the shape, since
`RuleOperator` has no concurrent member. Filed as
[#227](https://github.com/karlssberg/Motiv/issues/227) rather than built.
