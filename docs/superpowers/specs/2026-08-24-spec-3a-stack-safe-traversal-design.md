# Spec 3A — Stack-Safe Result Traversal — Design

**Date:** 2026-08-24
**Status:** Approved (design)
**Source:** Build step 1 of bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
resolving ticket [19](https://github.com/karlssberg/Motiv/issues/119) sub-questions 2 and 3. Opens the
Operability & Evidence bundle, as [#128](https://github.com/karlssberg/Motiv/pull/128) (Spec 2C) closed
Durability & Data.

## Summary

`Motiv` v8.0.0 has a live crash reachable from ordinary code, with no JSON and no HTTP:

```csharp
var combined = specs.Aggregate((a, b) => a.And(b));   // ~1,100 specs
var sources  = combined.Evaluate(model).RootAssertions;  // StackOverflowException — uncatchable
```

Ticket 19's `MaxCompositionDepth` fix guards `RuleDocumentParser`, which lives in the never-published
`Motiv.Serialization`. It protects a code path `Motiv` v8 consumers never take. The recursion it was
built to survive — nineteen non-tail walks over the result tree — is still there, in the published
package, and is the only genuinely shipped defect this bundle inherits.

This slice replaces every one of those walks with **one iterative post-order fold**, differential-tested
against the recursion it replaces. It does not change a single public signature, a single assertion
string, or a single node type.

## Measured today

Left-deep `And` chain of *N* minimal propositions, evaluated then read, Release build, on a 1 MB thread
(the ASP.NET request-thread stack — pool threads are what serve requests, so the small end governs).
Each row is the largest *N* for which the member returns rather than aborting the process.

| Member | Ceiling |
|---|---|
| `RootAssertions`, `SubAssertions`, `AllSubAssertions` | **1,038** |
| `UnderlyingAssertionSources`, `UnderlyingAllAssertionSources` | **1,038** |
| `Explanation.Underlying`, `Explanation.AllUnderlying` | **1,038** |
| `AllAssertions` | **1,891** |
| `UnderlyingExpressionResults`, `UnderlyingReasons` | **1,897** |
| `Reason` | **2,930** |
| `AllRootAssertions`, `Values`, `RootValues` | 2,000–4,000 |
| `UnderlyingMetadataSources` | > 4,000 |
| `Assertions`, `Justification` | ≥ 12,779 |
| `Satisfied` — i.e. `Evaluate` itself | **12,779** |

Two things fall out of that table, and they set this spec's scope.

**1. The asymmetry ticket 19 exists to prevent is already live.** Between 1,039 and 2,930 operands a
consumer gets `Reason` and `Justification` back but `RootAssertions` kills the process. Same object,
same evaluation. Ticket 19 argued a *recursion-plus-guard* fix would introduce that band; the
measurement says the band is already here, uncatchably, and the spread is nearly 3×. This is the
"what fired but not why" split, in the shipped package, today.

**2. `Evaluate` itself is recursive, and dies at 12,779.** No property can outlive that, because you
cannot read a result you could not build. Ticket 19's audit never measured it — the audit was of
*result-tree* walks, and this is a walk of the *spec* tree (`AndSpec.EvaluateSpec` → `Left.Evaluate` →
…). It is a real ceiling and a real follow-up, and it is **out of scope here** (see "Explicitly out of
scope").

So the goal this spec can actually deliver, stated precisely: **every public member of a result behaves
identically at every depth that can be evaluated at all.** After this slice the table above collapses
to a single row at 12,779, set by evaluation. That is uniformity — the invariant ticket 19 asked for —
and the residual ceiling is one number, in one place, with one ticket against it.

## Decisions (locked)

1. **Scope is every recursive walk reachable from a public member of a result** — `BooleanResultBase`,
   `BooleanResultBase<TMetadata>`, `Explanation`, `ResultDescriptionBase`, and the
   `AssertionExtensions` root-walk helpers. Not just Families A and B. `Reason`'s 2,930 is *lower* than
   several Family-A members, so leaving the description tree recursive would preserve the exact
   asymmetry the ticket forbids, merely with the ranking reshuffled.

2. **One primitive, not nineteen rewrites.** A single internal iterative post-order fold, parameterised
   by a descend-set and a combine step. Ticket 19 settled that this is single-dispatch, not a visitor:
   the per-node variation the walk needs is *which children are causal*, and that is already four
   memoized virtuals (`Causes` / `CausesWithValues` / `Underlying` / `UnderlyingWithValues`). The fold
   reads the same virtual the recursion read. No `Accept`/`Visit`, no node-type change.

3. **Memo write-back lives in the primitive, not in the callers.** Ticket 19 flagged this as mandatory
   rather than an optimisation, and it is the single easiest thing to get wrong nineteen times. Putting
   the read-and-write pair in the fold's signature makes "forgot to cache" unrepresentable —
   which is also how `UnderlyingMetadataSources`' missing `field ??=` gets fixed: there is one
   implementation left to omit it from, and it doesn't.

4. **Descend-set, not filter-after.** `combine` receives values only for the children the fold was
   asked to recurse into. Family A's shape is
   `children.SelectMany(c => c is IBooleanOperationResult ? f(c) : [c])` — the recursion deliberately
   *stops* at a non-operation child. A fold that computed `f` for every child would still be correct
   (`f` is total) but would turn an O(visited) walk into an O(whole-tree) one at every node. The
   descend-set preserves the pruning exactly.

5. **The oracle differential test is the acceptance gate, not a nice-to-have.** The current recursive
   code is a perfect oracle at depths where it does not overflow. Every rewritten member is asserted
   `SequenceEqual` against a captured recursive implementation over randomly generated trees —
   including short-circuited `AndAlso`/`OrElse` shapes with a null right operand, `Not` nests, `XOr`,
   higher-order results and expression-tree results. This converts "bugs live in the fold" from a
   standing risk into a checked invariant, which is the only reason a rewrite of the repo's
   most-depended-upon code is a responsible thing to do.

6. **No public signature changes. No behaviour changes.** Including the odd ones. Two are worth naming
   because they look like bugs and are deliberately preserved:

   - `UnderlyingMetadataSources` yields `this` — the *parent* — where its two Family-A siblings yield
     the *child* (`booleanResult.ToEnumerable()`), and it has no `.ElseIfEmpty(...)` fallback. That is
     a semantic divergence, not just the missing memoization ticket 19 named. Changing it is a
     behaviour change to a published property; the fold reproduces it verbatim and it gets its own
     ticket.
   - `Explanation.Underlying` collapses a level when the children's assertions equal the parent's. That
     post-order comparison is part of the fold's combine step, not something the driver knows about.

## Decision — the fold is a fold, not an enumerator

The tempting shape is `IEnumerable<(node, depth)>` — one iterative walker that yields nodes, and
nineteen callers that consume it. It is the wrong shape here, for three reasons found by reading the
code rather than by taste:

- **Every one of these walks is a post-order fold, not a pre-order visit.** The
  `.ElseIfEmpty(this.ToEnumerable())` at the end of each Family-A body is a per-node fold step: *if my
  children contributed nothing, I am the source*. A yielding enumerator hands the caller nodes and
  leaves it to rebuild the accumulator by hand — which is precisely where the bugs would live, and
  precisely what ticket 19 said a visitor would fail to help with.
- **The memo is per-node, so the accumulator must be per-node too.** A caller consuming a flat
  `(node, depth)` stream cannot write back a value for an interior node without reconstructing the
  parent–child boundaries the walker just discarded.
- **`yield return` reintroduces the cost the rewrite removes.** The measured churn today is
  `SelectMany`/`ElseIfEmpty` iterator chains allocated per node; a `yield`-based driver relocates that
  garbage rather than removing it. Ticket 19's allocation note makes a closure- and iterator-free inner
  loop a hard requirement, not a nicety.

So the primitive takes `descend` and `combine` and returns the root's value, and the loop that drives
it allocates one growable node stack and one growable value list per walk — reused across the whole
walk, not per node.

## Architecture

### `src/Motiv/Traversal/PostOrderFold.cs` (new, internal)

```csharp
internal static class PostOrderFold
{
    internal static TValue Fold<TNode, TValue>(
        TNode root,
        Func<TNode, IReadOnlyList<TNode>> descend,
        Func<TNode, IReadOnlyList<TValue>, TValue> combine,
        Func<TNode, TValue?> read,
        Action<TNode, TValue> write)
        where TNode : class
        where TValue : class;
}
```

- `descend(node)` — the children whose folded value `combine` needs, in order.
- `combine(node, values)` — the node's value, given those children's values in that order.
- `read`/`write` — the memo pair. `read` returning non-null prunes the walk at that node.

The five delegates are `static readonly` singletons per call site, so a walk allocates no closures.

**Frames.** One `struct` frame per node on the explicit stack: `(node, children, nextChildIndex,
valueBase)`. `valueBase` is the index into the shared value list where this node's children's values
begin, so `combine` gets a contiguous window and no per-frame list is allocated.

**Sharing.** Result trees are DAGs — the same result instance can occupy two positions. Depth-first
post-order completes a node before the walk can reach any later occurrence of it, and completion
writes the memo, so the second occurrence is pruned by `read`. Exponential blow-up on a diamond is
therefore not reachable, and no in-progress set is needed. This is the confirmation ticket 19 asked
for before iterating ("confirm first whether result nodes can be shared between positions").

**Growth.** Ticket 19's allocation note proposed sizing the working buffer from `MaxCompositionDepth`.
That does not hold: `MaxCompositionDepth` bounds documents parsed by `Motiv.Serialization`, and the
crash this spec fixes is reachable from hand-written C# that never touches a document. The buffers grow
by doubling from a small initial capacity instead. They are `O(depth)` and dwarfed by the tree they
walk.

### Result-tree walks (`BooleanResultBase`, `BooleanResultBase<TMetadata>`)

Six bodies swap from recursion to a `Fold` call. The `field ??=` backing stores become explicit private
fields so the memo pair can address them.

| Member | `descend` | `combine` |
|---|---|---|
| `UnderlyingAssertionSources` | `Causes` where operation | children's values interleaved with non-operation causes, `ElseIfEmpty([node])` |
| `UnderlyingAllAssertionSources` | `Underlying` where operation | same, over `Underlying` |
| `UnderlyingMetadataSources` | `CausesWithValues` where operation | same shape, but the non-operation arm yields `node` and there is no `ElseIfEmpty` — preserved verbatim per decision 6 |
| `UnderlyingExpressionResults` | `Causes` | pair-branching on `(node, child)`, unchanged |
| `AllAssertions` | `Underlying` when `IBinaryBooleanOperationResult` | concatenation, else `Assertions` |
| `UnderlyingReasons` | — | derived from `UnderlyingExpressionResults`; stack-safe once that is |

### `AssertionExtensions` root walks

`GetAssertions`, `GetAllAssertions`, `GetRootAssertions`, `GetAllRootAssertions` are lazy, un-memoised
`SelectMany` recursions — the un-memoised ones are why `RootAssertions` has the lowest ceiling in the
table *and* why probing it at depth 6,000 took minutes: the iterator chain is re-allocated on every
enumeration. They fold over the same trees; the fold's memo is a walk-local dictionary rather than a
node field, because these helpers take an arbitrary `IEnumerable<BooleanResultBase>` and have nowhere
on the node to cache.

### `Explanation`

`ResolveUnderlying` / `ResolveAllUnderlying` recurse through `Underlying` / `AllUnderlying` on the
collapse branch. Same fold, over `Explanation` nodes; the collapse comparison is the combine step.

### Description tree (`ResultDescriptionBase` and its twelve implementations)

Two recursions here, and they need different treatment.

- **`Reason`** — `BinaryBooleanResultDescription.Reason` recurses into `_causalResults[i].Description.Reason`,
  and `XOrBooleanResultDescription.ContainsBinaryOperation` recurses over `Underlying`. Both fold.
  `NotBooleanResultDescription.NegateNotOperator` already unwinds its `Not` nest with a `while` loop —
  it is the one place in the codebase that got this right, and it stays as it is.
- **`GetJustificationAsLines`** — twelve bespoke line formatters. They keep their formatting; what
  changes is that a node renders from its children's *already-rendered* line blocks rather than calling
  into them. Each description gains an internal child-selector and a combine over `IReadOnlyList<IEnumerable<string>>`,
  and `Justification` drives the fold. The `WithoutCausalCount` variant is a second mode, so the fold's
  memo is keyed on `(description, mode)`; the two modes are separate values, exactly as the two methods
  are separate today.

## Testing

### The oracle differential suite (`StackSafeTraversalOracleTests`)

The gate. A generator produces random result trees from a seeded PRNG over the full node vocabulary —
`And`, `Or`, `XOr`, `AndAlso` and `OrElse` (both the two-operand and the short-circuited one-operand
forms), `Not` nests, minimal / explanation / metadata propositions, higher-order results and
expression-tree results — at depths of 1–40, where recursion is far from overflow. For each generated
tree, every rewritten member is compared to a **captured copy of today's recursive implementation**,
held in the test project. `SequenceEqual` for sequences, `Equals` for strings.

Capturing the oracle in the test project rather than diffing against `git show` matters: it keeps the
oracle runnable in CI forever, so a later change that quietly alters traversal semantics fails a test
rather than passing review.

### Depth regression tests

One test per rewritten member, at a depth that aborts the process today (2,000 for the 1,038-ceiling
group), run on an explicitly-sized 1 MB thread so the test is not accidentally passing on an 8 MB main
stack. They assert a value comes back, not what it is — the oracle suite owns correctness.

### Allocation

Ticket 19 predicted transient churn should *fall*, conditional on a closure- and iterator-free inner
loop. `src/examples/Motiv.Benchmark` gains a case over a 200-node tree reading `Assertions`,
`RootAssertions` and `Justification`, so the claim is measured rather than asserted. The one member
whose allocation should fall visibly is `UnderlyingMetadataSources`, which today re-walks its whole
subtree on every access.

## Explicitly out of scope

- **Evaluation's own recursion (ceiling 12,779).** A spec-tree walk, not a result-tree one, reached
  before any property is read. It bounds every row in the table uniformly, so removing the result-tree
  recursion still delivers this spec's invariant. New ticket.
- **Raising `MaxCompositionDepth`.** Ticket 19 decided the cap should be "kept, re-derived and raised
  above 256". Deferred, deliberately: the cap's binding constraint after this slice is evaluation
  recursion, which this slice does not touch, and 12,779 is measured for the *thinnest* node type there
  is. Raising a limit on the strength of a ceiling that is still standing is the same cargo-cult the
  ticket warned against, pointed the other way. It gets re-derived by the ticket that makes evaluation
  stack-safe. The cap's *rationale* is restated in its XML docs now, since the stack argument it
  currently gives is about to stop being the reason.
- **A result-size bound counted inside the traversal loop.** Ticket 19 recommended this, "not yet
  confirmed", as the replacement for the crash that used to cap the amplification finding. Confirmed as
  the right idea, in the wrong place: the traversal walks a tree that evaluation has *already* built and
  paid for. A bound there refuses to read a result the process is already holding — it suppresses the
  report, not the cost. It belongs on the evaluation path, and travels with the evaluation ticket.
- **`UnderlyingMetadataSources`' parent-vs-child divergence.** Preserved verbatim; own ticket.
- The rest of bundle 3 — the decision log (15) is Spec 3B, the telemetry surface and PII control (04)
  is Spec 3C.

## Risks

| Risk | Mitigation |
|---|---|
| A subtle traversal-order or fold change slips through | The oracle suite compares against the actual recursive code over generated short-circuited trees; it is the acceptance gate, written and green before any body is swapped |
| The fold allocates more than the memoized recursion it replaces | Closure-free static delegates, one reused node stack and one reused value list per walk, benchmark case added |
| Twelve description formatters is the largest surface, and the most bespoke | Each keeps its own formatting verbatim; only the *source* of child lines changes, and each is covered by existing `Justification` assertions across `Motiv.Tests` and the three example test projects |
| A DAG with shared nodes degrades | Post-order completion writes the memo before any later occurrence is reached; asserted by a test that builds a shared-node result and counts combine invocations |
