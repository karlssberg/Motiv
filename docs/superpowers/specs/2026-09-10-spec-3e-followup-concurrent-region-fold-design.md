# Design — the fan-out that was folded by nesting it

Source: bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 *Structural safety (19)*, §4 (*"no uncatchable crash, bounded work"*), §7.
Ticket [#145](https://github.com/karlssberg/Motiv/issues/145), residual 2 of
[Spec 3E](https://github.com/karlssberg/Motiv/pull/144) · residual 1 landed as
[#203](https://github.com/karlssberg/Motiv/pull/203) · budget
[#202](https://github.com/karlssberg/Motiv/issues/202) ·
[#204](https://github.com/karlssberg/Motiv/issues/204).

## What was wrong

Spec 3E folded the logical operators onto the heap so that `Evaluate`, `Matches`, `EvaluateAsync` and
`MatchesAsync` are flat at any composition depth, and named two shapes it had not reached. This is the
second: `AsyncAndSpec`, `AsyncOrSpec` and `AsyncXOrSpec` take a `concurrent` flag that evaluates both
operands through `Task.WhenAll`, and the driver declined such a node — `IAsyncFoldableOperation`
carried an `IsConcurrent` flag whose whole meaning was *"not mine; evaluate yourself"*.

So a nest of them recursed once per layer, and each layer was expensive: `AsyncConcurrentFanOut`'s
state machine, plus `Task.WhenAll`, plus each operand re-entering evaluation through its own entry
point. Measured out of process on a 1 MB thread the last depth that returned was **669** when the
ticket was written and **between 400 and 450** on .NET 9 Release when this slice re-measured it. Past
that the process aborts with a stack overflow no `catch` can see, which is the crash class §4 of the
bundle spec exists to close.

The ticket's own framing was that fixing it meant "a genuinely parallel fold — a different algorithm,
not a variation on the existing one."

## Decision 1 — the region, and why this is not a parallel scheduler

It is a variation on the existing one, because of what a nest of concurrent operators already means.

```
a.AndConcurrently(b.AndConcurrently(c))
```

starts `a` and the inner node together; the inner node starts `b` and `c` the instant it is reached.
All three operands are in flight either way. **The nesting was never the source of the concurrency,
only how it was spelled** — which means flattening it is not a trade of concurrency for stack safety.
It preserves the concurrency exactly and drops the per-layer `Task.WhenAll` and the stack frame that
came with it.

So `AsyncEvaluationFold.FoldConcurrentlyAsync` absorbs an unbroken run of concurrent operations into
one **region**, starts every operand at that region's boundary in a single `Task.WhenAll`, and composes
the region from the answers afterwards.

The alternative — one loop driving several cursors at once, with a completion queue and a signal — is
the parallel fold the ticket imagined. It buys nothing here: the region already starts everything at
once, and a scheduler's extra reach is over *sequential* operations interleaved with concurrent ones,
which is the residual this slice files rather than the one it fixes (below).

## Decision 2 — the region's shape is read, not evaluated

The walk visits a node's operands without knowing any outcome, which is exactly what the frame machine
may not do: a frame asks for its second operand only once the first has answered, and that is what
makes short-circuiting expressible at all.

What licenses it here is that **concurrency implies eagerness**. `AndConcurrently` and its siblings
start both operands regardless of outcome — there is no concurrent short-circuiting variant, and the
published docs say why — so `NextOperand` returns the same operand whichever outcome it is told. The
region's shape is therefore a property of the composition rather than of the run.

That is now written into `IAsyncFoldableOperation.IsConcurrent`'s contract, because it has stopped
being an incidental truth about three classes and become something a fold depends on.

The walk is breadth-first by construction — a `List` appended to while being indexed forward — so it
costs no stack of its own, and every node's operands land at a higher index than the node. That is
what lets the composing pass run *backwards* over one array with no second traversal and no ordering
work: by the time a node is reached, both its children have values.

Operand placements are packed into one `int` per slot: a region index is non-negative, a boundary
index is its bitwise complement. There is no third value for an operand a node does not have, because
a concurrent operation is binary and eager by contract — an early draft carried one, and it was the
only code in the method no test could reach.

## Decision 3 — the region charges the nodes it walks

`MaxEvaluationSize` never saw a concurrent nest. A fan-out reached from a fold was charged by that
fold as an operand; the fan-out charged neither its operands nor the concurrent nodes beneath it,
because `EnterFanOut` existed precisely to *not* charge a node the fold above had already charged.
A nest of any depth therefore cost the budget **one node**.

That was invisible while the stack capped the depth at a few hundred. Removing the cap is what makes
it matter: an unbounded shape with no bound on it is the other half of §4's "bounded work", and
shipping the stack fix without the accounting fix would have traded a crash for an unbounded
evaluation.

So the region charges each concurrent node it absorbs and each operand it answers itself. It does not
charge an operand it hands to a fold of its own — that fold charges its root on entry, and charging
here as well would count it twice. `EnterFanOut` is deleted: with the fold walking the whole region,
the ordinary root-and-operand accounting covers both the case where a concurrent node is the whole
evaluation (charged by `EnterAsync` as the fold's root) and the case where it is reached from above
(charged once, as an operand).

The four cases that pinned the old arithmetic — 15, 15, 14 and 13 nodes — are unchanged, because every
boundary in them is a fold. Two new cases pin the nest: four leaves under three concurrent operations
cost seven, where they used to cost one.

## Decision 4 — the flag stays, and three operators lose a branch

`IsConcurrent` could have been read as "not foldable" and removed once folding covered it. It means
something sharper now — *eager, and unordered with respect to its operands* — which is a property of
the operation, so it stays on the seam and the fold dispatches on it.

What goes is the branch that used to shadow it. All six entry points across `AsyncAndSpec`,
`AsyncOrSpec` and `AsyncXOrSpec` collapse from an `if (!concurrent)` fork into a single delegation to
the fold, and `AsyncConcurrentFanOut` — which existed because those six were otherwise six copies of
the same fan-out — is deleted with them.

## Verification

- `DeepEvaluationTests` — all three concurrent operators and both entry points at 50,000 operands on a
  1 MB thread, the same bar the sequential chains are held to. Out of process the probe returns at
  50,000 where it aborted past 450.
- `ConcurrentOperatorTests` — a nest's `Satisfied`, `Reason`, `Assertions` and `Justification` against
  its sequential twin, and a three-operand nest whose operands each wait for the other two, so the
  composition completes only if the flattening left them all in flight.
- `AsyncEvaluationBudgetTests` — the nest costs seven nodes and is refused at six.
- `DecoratorNestingTests` — the `Concurrent(160)` case retires; its two rows in that suite's measured
  table are marked removed rather than deleted, so the history of what was measured stays readable.
- Full solution suite green on net8.0, net9.0 and net10.0. net472 is built, not run, on this host.

## Not done here — and the number it is filed with

**Alternating concurrent and sequential operations** — [#227](https://github.com/karlssberg/Motiv/issues/227). A sequential operation at a region's boundary is
handed to a fold of its own, so `x.AndConcurrently(y).And(z)` repeated costs a frame per alternation.
Bisected out of process on the same 1 MB thread, that shape now returns to **298** layers.

It is filed rather than built because closing it is the scheduler of Decision 1 — one loop holding
several live cursors, with a completion queue and a signal — and because the shape is not reachable
from a rule document: `RuleOperator` has thirteen members and none of them is concurrent, so this
depth is what an author writes by hand, in a mixture of two operator families, on purpose.
