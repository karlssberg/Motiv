# Design — a stack probe at the region's boundary

Ticket [#227](https://github.com/karlssberg/Motiv/issues/227). Plan:
[`2026-10-04-spec-227-alternating-composition-stack-probe.md`](../plans/2026-10-04-spec-227-alternating-composition-stack-probe.md).

## The question

A composition alternating concurrent and sequential operators recursed once per alternation, because
an operand at a concurrent region's boundary that is itself a sequential operation is answered by a
fold of its own — it must run concurrently with its siblings, and the region has no way to interleave
two ordered walks. The [region-fold design](2026-09-10-spec-3e-followup-concurrent-region-fold-design.md)
filed it on the grounds that closing it meant a scheduler: one loop driving several live cursors, with
a completion queue and a signal, paying its allocation on every asynchronous evaluation that reaches a
concurrent node.

## Decision: don't remove the nesting, move it off the stack when the stack runs out

The nesting is not the defect; spending the thread's stack on it is. An async method that is started
on a fresh stack keeps its caller's state on the heap — the awaiting state machines — so a nested fold
started elsewhere costs the original thread nothing.

So the region starts each boundary operand inline, exactly as before, while
`RuntimeHelpers.TryEnsureSufficientExecutionStack()` reports room, and through `Task.Run` once it does
not. The probe is a comparison against a limit the runtime keeps, so the common path — every
composition that ever worked — pays one comparison per boundary operand and nothing else.

### What it costs, and where

- **Only past the point that used to abort.** Below it, a boundary operand starts on a thread-pool
  thread rather than the caller's. The fold already awaits with `ConfigureAwait(false)` throughout, so
  no caller could rely on staying on its context; the change is that a synchronous leaf past the
  threshold also runs off the caller's thread.
- **The budget is unchanged.** The asynchronous `EvaluationBudget` counter lives in an `AsyncLocal`,
  `Task.Run` flows the `ExecutionContext`, and charges are interlocked, so a hopped fold charges the
  same counter. Tested exactly at the limit and one node short.
- **Cancellation is unchanged.** The token goes to the operand, not to `Task.Run`, so a token
  cancelled before the hop is observed by the operand as it would be inline.

### Why not the scheduler

It would remove the nesting where this only relocates it, and it would charge every concurrent
evaluation for the alternating shape, which no rule document can compose (`RuleOperator` has no
concurrent member). The probe charges that shape alone, and only once it is deep enough to need it.

## Not done here

The probe sits at the concurrent region's boundary because that is where #227's recursion is. A
decorator that is not foldable still recurses as
[#203](https://github.com/karlssberg/Motiv/pull/203) measured and documented; this does not change it.
