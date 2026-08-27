# Spec 3E — Stack-Safe Evaluation — Design

**Date:** 2026-08-27
**Status:** Approved (design)
**Source:** The remainder of ticket [19](https://github.com/karlssberg/Motiv/issues/119) in bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md).
Tracked as [#135](https://github.com/karlssberg/Motiv/issues/135). Follows
[#134](https://github.com/karlssberg/Motiv/pull/134) (Spec 3A), which made every *result*-tree walk
stack-safe and named this ceiling as the one it was leaving standing.

## Summary

Spec 3A delivered a uniformity invariant — no public member of a result has a lower depth ceiling than
any other — and left one ceiling above them all: evaluation itself. `AndSpec.EvaluateSpec` calls
`left.EvaluateInternal(model)`, which calls the next `AndSpec.EvaluateSpec`, all the way down. The walk
is over the **spec** tree rather than the result tree, and it happens before any property is read, so it
bounds every member uniformly — which is exactly why 3A could deliver its invariant without touching it.

Measured here, Release, on a 1 MB thread (the ASP.NET request-thread stack), over
`specs.Aggregate((a, b) => a.And(b))` of the thinnest node type there is:

| Entry point | Last depth that returns | First that aborts the process |
|---|---|---|
| `Evaluate` | 12,786 | 12,787 |
| `Matches` | 21,494 | 21,495 |
| `EvaluateAsync` | **633** | 634 |

Three ceilings, not one, and the ticket named only the first.

`Matches` is the allocation-free boolean path and has its own recursion —
`left.Matches(model) & right.Matches(model)` — with thinner frames, so it survives longer and fails the
same way. `EvaluateAsync` has its own too, and it is *twenty times worse*: `await` on a
synchronously-completing `ValueTask` resumes on the same stack, and an async state-machine frame is far
fatter than a call frame. Ticket 19's audit covered none of the three, because the audit was of
result-tree walks.

The async number is the one that changes what this slice has to do. `MaxCompositionDepth` defaulted to
256 — below 633, so async was safe by accident. Raising it, which is what ticket 19 asked for and this
ticket was meant to unblock, would have handed a rule document a way to abort an async host. So async is
in scope: raising the cap and folding async are the same decision.

This slice makes all three iterative for the **logical-operator family**: `And`, `Or`, `XOr`, `AndAlso`,
`OrElse` and `Not`, across their spec, policy, expression-tree and async variants — twenty-seven classes,
a closed set. It also lands the **result-size bound** ticket 19 asked for and Spec 3A explicitly deferred
to this ticket, and re-derives **`MaxCompositionDepth`**, which is now defensible.

## Decisions (locked)

### 1. The seam is the operation itself, not a visitor

Spec 3A's `PostOrderFold` consumes an existing abstract seam — `Causes` / `Underlying` — and needs
nothing from the node types. There is no equivalent here. `IBinaryOperationSpec<TModel, TMetadata>`
exposes `Left` and `Right`, so the *descent* is already abstract, but the two things a driver needs
beyond descent are not:

- **which operands to evaluate**, which for `AndAlso` and `OrElse` depends on the first operand's
  outcome; and
- **how to combine their results**, which differs per operator and, within an operator, between the
  spec and policy variants — `AndAlsoSpec` builds an `AndAlsoBooleanResult`, `AndAlsoPolicy` an
  `AndAlsoPolicyResult`, and the policy one is what preserves policy-ness through a short-circuit.

So each operation implements a small internal seam, `IOperationFold<TModel, TMetadata>`, with four
members: `FirstOperand`, `NextOperand(bool firstSatisfied)`, `Combine(first, second)` for the result
path and `CombineMatches(first, second)` for the boolean one. `EvaluateSpec` and `Matches` then become
one-line calls into the driver.

A visitor was rejected for the reason 3A rejected it: it would mean an `Accept`/`Visit` pair and a
node-type enumeration in the driver, where single dispatch through a seam each class already almost
implements costs nothing and cannot go stale — a new operator that forgets to implement the seam does
not compile.

`NextOperand` takes a `bool` rather than the first operand's result. Short-circuiting is a question
about satisfaction, and phrasing it that way is what lets one descent seam serve both the result fold
and the boolean fold. It is also the reason `Not` fits: a unary operation is a binary one whose
`NextOperand` is always `null`.

### 2. One driver, two value types

`Evaluate` folds `BooleanResultBase<TMetadata>`; `Matches` folds `bool`. Everything else about the two
walks — the frame stack, the growth, the order, the short-circuit decision, the size bound — is
identical.

Writing them as two loops would duplicate the one part of this slice where a bug could hide silently:
the control flow. A `Matches` fold that pushed frames in a subtly different order would still return the
right answer on every test that does not also assert *which* operands were evaluated. So there is one
generic loop, parameterised by a `struct` driver the JIT specialises away, supplying the three things
that genuinely differ: how a leaf produces a value, how a value's satisfaction is read, and how a node
combines.

This is parametric, not branching. CLAUDE.md's warning about over-DRYing is about abstractions with
branching logic inside them; there is no `if` in the driver that asks which fold it is running.

### 3. The fold drives the combinator family and calls everything else

The driver descends through operands that are themselves `IOperationFold` and calls `EvaluateInternal`
(or `Matches`) on operands that are not — decorators, higher-order propositions, `ChangeModelTypeSpec`,
user-defined `Spec` subclasses, leaves.

That is the honest bound, and it is worth stating rather than glossing: **a chain of combinators is now
flat at any depth; a chain of alternating decorators still costs a frame per decorator layer.** The
distinction matters because of where depth comes from. The threat model's crash
([ticket 05](https://github.com/karlssberg/Motiv/issues/105)) is a flat operand array folded left-deep
by `RuleBinder` — pure combinators, attacker-controlled length. Decorator nesting comes from how many
propositions an author wraps around each other, which is bounded by the catalogue rather than by a
request body.

Extending the seam to decorators would mean giving `ChangeModelTypeSpec` a place in a fold whose model
type is fixed and `MetadataToExplanationAdapterSpec` a place in one whose metadata type is — neither of
which the driver's type parameters admit. That is a different design, and it buys a bound on a depth
nothing drives.

### 4. Async is folded too; concurrency is not

`AsyncAndSpec` and its eight siblings get the same treatment through a parallel seam and driver,
`IAsyncOperationFold` / `AsyncEvaluationFold`. Two things differ, both forced by `async`:

- **Frames are indexed, not `ref`-ed.** An async method cannot hold a by-ref local across an `await`.
  Array element access is a variable either way, so the frames are still mutated in place.
- **No per-thread buffer.** A continuation may resume on a different thread than the one that took the
  buffer, so it would be returned to the wrong thread. An async evaluation already allocates a state
  machine per awaited operand, next to which one array is not the cost worth chasing — which is also why
  the buffer *was* worth it on the synchronous side, where `Matches` allocated nothing at all.

The two drivers are deliberately *not* merged. Their frames and seams could share a type parameter, but
the loops cannot: one has to be `async`, and making the synchronous path async would cost it a state
machine per operand — the thing decision 7's buffer exists to avoid. Merging the halves that can merge
would leave the halves that matter duplicated anyway, behind one more type parameter, so each driver
stands alone, each with its own depth, order and short-circuit cases so a divergence fails a test rather
than shipping.

What is **not** folded is the `concurrent` flag on `AsyncAndSpec`, `AsyncOrSpec` and `AsyncXOrSpec`,
which evaluates both operands through `Task.WhenAll`. That is a fan-out rather than a walk; an iterative
driver for it is a genuinely parallel fold, a different algorithm. The fold leaves such a node to
evaluate itself, and the node's own recursion is bounded by how deeply an author nests
`AndConcurrently` by hand — because `AsyncRuleBinder` composes only sequential operators, so a rule
document cannot produce one at all.

### 5. The result-size bound goes on the evaluation path, and counts what the fold builds

Ticket 19 recommended a result-size bound "counted inside the traversal loop", as the replacement for
the crash that used to cap [ticket 05](https://github.com/karlssberg/Motiv/issues/105)'s amplification
finding. Spec 3A confirmed the idea and rejected the placement — a bound in the traversal refuses to
*read* a tree the process is already holding, which suppresses the report rather than the cost.

It lands here, in the evaluation fold, counting the nodes one top-level evaluation composes — one per
proposition evaluated and one per operation joining them, so a chain of *n* propositions is *2n - 1*
nodes. That is the quantity the attack controls, and it is paid whether or not anything ever reads the
result.

The bound is not free of judgement, and two things about it are deliberate:

- **It is a backstop, not a validator.** `Motiv.Serialization`'s `MaxCompositionDepth` is where a
  document is refused, with a message naming the document. This one fires in the engine, after binding,
  and its message can only name a count. A deployment that relies on this instead of on the parser has
  its guard in the wrong place.
- **It counts combinator results only.** Higher-order amplification happens inside a leaf, where the
  fold cannot see it. Claiming this bounds all evaluation work would be false; it bounds the shape the
  removed crash used to bound, which is the claim ticket 19 actually made.

Configuration follows `MotivTelemetry.ExplanationDetail`: a process-wide static with a documented
"set it once at startup". Motiv has no options object to hang it on and inventing one for a single
integer would be a bigger API commitment than the setting deserves.

The default is derived rather than picked. A node of the thinnest composition there is costs about 190
bytes of retained result — measured over left-deep `And` chains from 1,000 to 100,000 propositions,
where the per-operand figure holds at 376–382 bytes across three orders of magnitude — so 250,000 nodes
puts one evaluation's ceiling near 50 MB. That is a chain of 125,000 propositions: far above anything an
author writes, far below what a request body should be able to spend.

### 6. `MaxCompositionDepth` is re-derived, not merely raised

Ticket 19 decided the cap should be "kept, re-derived and raised above 256"; Spec 3A deferred that
because its binding constraint was this ceiling, and raising a limit on the strength of a ceiling that
is still standing is cargo-culting pointed the other way.

With evaluation flat, the binding constraint becomes the one this slice introduces — the result-size
bound — plus the memory a bound document costs. The new number is derived from that, and the XML docs
say which constraint it is derived from, so the next person to move it knows what to re-measure.

### 7. The frame buffer, and the two things the build found

Two things came out of building this that the design above did not predict, and both belong in it.

**A fold allocates where recursion did not.** The frame stack is an array, and a three-operand
composition — the ordinary case — was paying one per evaluation: `Evaluate` went from 952 to 1,232
bytes per call, and `Matches`, whose documented contract is that it allocates nothing, went from **0 to
152**. Spec 3A's note said "measure before pooling"; measured, it pools. One buffer per thread per fold,
*taken* rather than borrowed so that a fold re-entered through an operand's own evaluation cannot
overwrite the frames its caller is still unwinding, cleared on return so a cached buffer pins no
results, and not cached at all above 64 frames so a deep evaluation does not leave a two-megabyte array
on the thread forever. Both figures return exactly to baseline.

**A policy's `Value` was recursive too, and only reachable once evaluation was not.** `OrElsePolicyResult`
and `AndAlsoPolicyResult` resolve `Value` as `(Right ?? Left).Value` and `NotPolicyResult` as its
operand's — a chain as deep as the composition. Before this slice no one could build a composition deep
enough to overflow it, because evaluation died first; the first 50,000-operand policy chain this slice
made evaluable then died reading the one member that makes a policy a policy. Same fix, smaller shape:
the selection chain is walked in a loop.

## What this does not do

- **Concurrent async operators.** `AndConcurrently` and its two siblings evaluate themselves, per
  decision 4. Unreachable from a rule document.
- **Decorator nesting.** A frame per wrapping layer, per decision 3.
- **The metadata tier's quadratic-plus cost** ([#137](https://github.com/karlssberg/Motiv/issues/137))
  and the projection cost it probably explains
  ([#139](https://github.com/karlssberg/Motiv/issues/139)). Both are about how much work a *shape*
  costs, not about whether a depth returns.
- **`UnderlyingMetadataSources`' parent-vs-child divergence**
  ([#136](https://github.com/karlssberg/Motiv/issues/136)), which wants a decision rather than a patch.
- **Bounding higher-order amplification.** Named as out of the size bound's reach in decision 5 rather
  than quietly implied to be within it.

## Verification obligations

- A composition far deeper than either measured ceiling evaluates and matches on a 1 MB thread.
- Every existing test passes unchanged: the fold is an evaluation-order rewrite, and short-circuiting,
  policy-preservation and justification output are all observable, so a behavioural difference shows up
  as a failure rather than as a judgement call.
- Short-circuiting is asserted directly — the operand of an `AndAlso` whose left is unsatisfied is never
  evaluated — rather than inferred from the result, since the fold is precisely the code that could
  start evaluating it. Asserted on both the synchronous and the asynchronous fold.
- The cancellation token reaches every operand of a folded async chain, not just the first two: it was a
  parameter threaded down the recursion and is now a local in the driver.
- A concurrent async operator still evaluates both operands, since it is the one shape the fold leaves
  alone.
- Exceeding the size bound throws with a message naming the limit and the setting; the default admits
  every composition the test suite builds.

## Outcome (recorded after the build)

Measured the same way as the table above — Release, 1 MB thread, left-deep `And` chain of minimal
propositions.

**All three ceilings gone.** 50,000 operands evaluate, match, evaluate asynchronously, and read back
through every member on a 1 MB thread — where 12,787 aborted the process synchronously and **634** did
asynchronously. The residual recursion is decorator nesting and the concurrent operators, both named
above rather than glossed, and neither reachable from a rule document.

**Allocation unchanged.** Three-operand chain, 100,000 iterations:

| | Before | Fold, unbuffered | Fold, buffered |
|---|---|---|---|
| `Evaluate` | 952 B/op | 1,232 B/op | **952 B/op** |
| `Matches` | 0 B/op | 152 B/op | **0 B/op** |

**`MaxCompositionDepth`: 256 → 4,096.** Re-derived against cost per evaluation now that stack is not
the constraint on any of the three paths: 4,096 evaluates in ~2.7 ms synchronously and ~1.8 ms
asynchronously and retains ~1 MB, where 16,384 costs 7–9 ms. Async is no longer the outlier it was — at
256 it cost 1.7× the synchronous path and at 633 it died. It stays below
`MaxNodeCount`'s implicit ceiling (a 10,000-node document cannot compose much past 10,000) so the two
caps remain independently meaningful, and far below `MotivLimits.MaxEvaluationSize`, which is the
engine's backstop for compositions that never came from a document.

**One test's premise expired.** `Should_refuse_a_document_that_would_compose_past_the_stack` asserted a
400 for 2,000 operands. Both halves of its name are now wrong — the cap is not about the stack, and
2,000 is inside it — so it is renamed, raised past the new cap, and paired with the case it could never
have had before: a 2,000-operand document that now answers 200.

