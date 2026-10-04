# Plan — the alternation that was folded one stack frame at a time

Ticket [#227](https://github.com/karlssberg/Motiv/issues/227), the residual
[#145](https://github.com/karlssberg/Motiv/issues/145)'s region fold filed rather than built. Design:
[`2026-10-04-spec-227-alternating-composition-stack-probe-design.md`](../specs/2026-10-04-spec-227-alternating-composition-stack-probe-design.md).

## The defect

`AsyncEvaluationFold.FoldConcurrentlyAsync` starts every operand at a region's boundary. One that is a
*sequential* operation is answered by a fold of its own, entered on the same stack, so
`spec.AndConcurrently(leaf).And(leaf)` repeated nests one fold per alternation. #227 measured the last
depth that returns on a 1 MB thread at **298**; past it the process aborts.

## Steps

1. **Red.** `DeepEvaluationTests` gains the alternating shape for all three concurrent operators at
   10,000 alternations, on `EvaluateAsync` and `MatchesAsync`. Before the fix the run aborts with a
   stack overflow inside `FoldConcurrentlyAsync → Place → LeafAsync → FoldAsync`.
2. **Green.** The region fold starts a boundary operand through `Start`, which starts it inline while
   `RuntimeHelpers.TryEnsureSufficientExecutionStack()` says the stack has room, and through `Task.Run`
   on a fresh thread-pool stack once it does not. `netstandard2.0` has only
   `EnsureSufficientExecutionStack`, so that build catches its `InsufficientExecutionStackException`.
3. **The budget across the hop.** `AsyncEvaluationBudgetTests` admits a 2,000-alternation composition
   at exactly its cost and refuses it one node short, on the 1 MB thread so that the hop happens.
   Mutation-checked: suppressing `ExecutionContext` flow into `Task.Run` makes the refusal case fail.
4. **The unwinding direction.** One more case runs the shape over leaves that complete
   asynchronously, so the folds complete through continuations rather than returns.
5. Full `Motiv.Tests`, `Motiv.Serialization.Tests`, `Motiv.Serialization.Snapshots.Tests` and the
   Poker, ECommerce and SmartHome example suites, `net10.0`.
