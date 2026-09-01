# Spec 3A follow-up — the deepest tier is a property of a branch — Design

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1. Ticket [#189](https://github.com/karlssberg/Motiv/issues/189); the plan is
[here](../plans/2026-09-01-spec-3a-followup-root-values-branches.md).

Spec 3's §7 asks that a traversal *"returns identical output to the recursive oracle"*. `RootValues`
did not, on a bounded and exactly-characterised set of nodes, and had not since before 3A. This slice
closes that.

## What changed

`GetRootValues` stops descending `MetadataNode.Underlying`:

```csharp
-        RootValuesOf(result.MetadataTier.Underlying)
-            .ElseIfEmpty(result.MetadataTier.Metadata)
-            .DistinctWithOrderPreserved();
+        RootValuesOf(result.MetadataTier).DistinctWithOrderPreserved();
```

and descends a new internal `MetadataNode.Branches` — the node's direct children, un-collapsed —
falling back to a node's own metadata when *that node* has no children, rather than when the whole
level came out empty:

```csharp
        return PostOrderFold.Fold(
            tier,
            node => node.Branches,
            (node, folded) => folded.Count == 0
                ? node.Metadata.ToArray()
                : folded.Flatten(),
            …);
```

`Branches` is `Resolution.Children` — already computed, already memoised — guarded for the leaf
constructor, which has no causes and never resolves. Nothing else moves. `MetadataTier.Underlying`,
`Explanation`, `Resolution` and the collapse rule are all untouched.

## The ticket named the wrong suspect, and proving it is the design

#189 pointed at `MetadataNode.Resolve`'s collapse rule, and the pointing was well-reasoned: that is
demonstrably where the two operands are lost. The dump of the failing node confirms it exactly —

```
AndBooleanResult :: greater than 2 & greater than 4 & (all neighbours == true)
  tier.metadata   = [greater than 2 | greater than 4 | n != k]   <- collapses: children restate me
  tier.underlying = 6 tiers, every one [n != k]                  <- all from the higher-order side
      greater than 2   -> leaf tier, no underlying               <- spliced to nothing
      greater than 4   -> leaf tier, no underlying               <- spliced to nothing
      all neighbours   -> 6 tiers deep
```

The obvious fix follows directly: when collapsing, let a childless child stand in for itself. It
works — the acceptance test goes green and the whole of `Motiv.Tests` passes.

It is still wrong, and the reason is only visible from the twin. `Explanation` is the same tree with
the same `Resolution<T>`, the same collapse and the same `Underlying`. Applying the identical rule
there fails **148 tests**, because that tree's emptiness is a stated contract:

```csharp
DeepAnd().SubAssertions.Count().ShouldBe(0,
    "a composition of atomic propositions has no layer beneath its own assertions");
DeepAnd().Explanation.Underlying.Count().ShouldBe(0, "as for SubAssertions, which projects it");
```

So `Underlying` means *levels strictly below me that say something new*, and collapsing to nothing is
the correct answer, not a bug. The metadata tier is that tree's twin and owes the same answer — its
version of that contract simply has no test, which is exactly the condition under which a plausible
fix ships and a public property quietly changes shape.

**A fix that passes because the contract it breaks is untested is not a fix.** The twin is what made
that legible here; without it the first attempt would have shipped green.

## The actual defect: `Underlying` cannot express a per-branch depth

Once the collapse is granted as correct, the fault has to be in the consumer, and it is a type error
of sorts. `Underlying` is lossy in two independent ways:

| | What it does | Why it is right | Why the walk cannot use it |
|---|---|---|---|
| Collapses | drops a level restating its children | `SubAssertions` must be empty when nothing is deeper | a branch whose deepest tier *is* that level leaves nothing behind |
| Flattens | returns one list of tiers | consumers want levels, not shape | which branch a tier came from is gone |

"The deepest tier" is a property **of a branch**, and `Underlying` is a property **of a level**. Asking
a flat list of levels for a per-branch answer cannot work in general; it happened to work for every
composition whose branches all bottom out at the same depth, which is every composition in the library
except one — a higher-order result, the only node type that expands a single cause into a subtree of
its own.

That is why the boundary in the ticket is so clean. It is not that higher-order results are
mishandled. It is that they are the only way to get a branch deep enough for a sibling's collapse to
be visible.

## Why the old fallback hid it

```csharp
RootValuesOf(result.MetadataTier.Underlying).ElseIfEmpty(result.MetadataTier.Metadata)
```

The fallback is right, and it is applied at the wrong granularity. `a & b` has both branches bottom
out at once, `Underlying` is empty, the fallback fires, and the answer is correct. Put one deep
sibling next to them and the level is no longer empty, the fallback never fires, and the two shallow
branches are simply gone.

**A collection-level fallback masks per-element loss exactly when the elements are heterogeneous** —
and heterogeneity is rare, so the mask holds until it doesn't. Per-branch is now the only granularity
in the walk, so there is no level-wide fallback left to be right by luck.

## Measurement

Over the Spec 3A corpus — 13,680 nodes across 150 seeds, comparing the new walk against the old one
rebuilt from the public `MetadataTier` surface (which the fix leaves unchanged):

| | Count |
|---|---|
| Nodes whose `RootValues` changes | **2** |
| …of those, in a subtree containing a higher-order result | 2 |
| …in a subtree containing none | 0 |
| Nodes that **lose** a value | **0** |
| Nodes that gain one | 2 |

Two nodes out of 13,680 is the whole blast radius, and it is exactly the count #189 reported. The
zero in the fourth row is the row that matters: the change is strictly additive per node, so no
consumer can see a value disappear. That is the same shape #136's repair had — *"every one of them
strictly gaining values it had been dropping, none losing any"* — and it is what one expects of a walk
that was under-reporting rather than mis-reporting.

The corpus does not reach every case, which is the point of the next section.

## The twin, unfixed and measured

`RootAssertions` has the same defect, by the same mechanism, against the same oracle:

| Nodes | Disagreements with an independent causal-leaf descent |
|---|---|
| Subtree contains a higher-order result (2,063) | **84** |
| Subtree contains none (11,617) | **0** |

That is the same boundary #189 reported for `RootValues`, which is what one would expect of the same
bug in the twin tree. Left out deliberately, and not out of caution: `RootAssertions` and
`AllRootAssertions` are named by 46 lines across the test suites and the example projects against 15
for `RootValues`, and the assertion tree additionally feeds `SubAssertions` and the description
formatters. The two changes are not the same size and do not belong in the same review. Filed as
[#192](https://github.com/karlssberg/Motiv/issues/192), which carries the diagnosis and the
148-test refutation so the next session does not re-derive them.

The chain is worth reading whole: 3A deferred the parent-vs-child divergence to #136, #136 deferred
the fallback-to-self to #188, #188 declined `RootValues`' own fallback and named #189, and #189 ends
by naming #192. Each link is a question the previous slice could see but could not afford.

## Cost

`Branches` never collapses, so the walk visits more nodes than `Underlying` did. Measured on `And`
chains, before and after, in Release:

| Operands | Before | After |
|---|---|---|
| 250 | 7 ms | 6 ms |
| 500 | 15 ms | 15 ms |
| 1,000 | 45 ms | 40 ms |
| 2,000 | 150 ms | 150 ms |

Unchanged. The quadratic-plus edge count of the tier tree over a fully-causal `And` chain — noted in
`DeepCompositionTests` and predating 3A — dominates, and the walk over it does not. The small-stack
depth ceiling is unchanged too: `Should_read_RootValues_of_a_deep_composition` passes on its 1 MB
thread.

## What the review pass found

The `code-simplifier` round applied three edits, all clarity: `Branches` returned the
`EmptyUnderlying` field, which is named for the cache backing `Underlying` and made a second property
look like it shared it; the `RootValuesOf` remark restated `Branches`' own rationale, so two copies of
one explanation were left to drift apart; and the new corpus-premise guard counted all 13,680 nodes to
prove one exists, where its exact sibling in `UnderlyingSourcesFallbackTests` short-circuits.

It also found a **second instance of this very defect**, which is the round's real yield.

The fold as first written fell back when a branch had **no children**. Its assertion twin,
`AssertionExtensions.CombineRootAssertions`, falls back when the branch **contributed nothing** —
it flattens first and tests the flattened result:

```csharp
var rootAssertions = foldedUnderlying.Flatten();
return rootAssertions.Length == 0 ? AsArray(explanation.Assertions) : rootAssertions;
```

The two agree everywhere except one shape: a branch that *has* children but whose whole subtree yields
no metadata. The review noted it was unreachable in the corpus — true — and left the call deliberate
rather than accidental. It is reachable in user code, and the divergence is #189 again one level up:

```csharp
Spec.Build(yieldsNothing).AsAllSatisfied().WhenTrue("all yielded nothing")…
```

A higher-order proposition carries its own `WhenTrue` value independently of its operands, so when
those operands yield an empty sequence the branch has children, contributes nothing, and — under the
childless-only fallback — drops the value it does have. `RootValues` reported `["sibling-true"]` where
`["all yielded nothing", "sibling-true"]` is correct.

That is now `Should_fall_back_for_a_branch_whose_subtree_yields_no_values`, written failing first, and
the fold matches its twin's form. **The lesson generalises past this fix:** the review's own reasoning
for leaving it — the corpus cannot reach it — is the reason it needed a hand-written test rather than
a decision. A generated corpus establishes a boundary; it does not enumerate the shapes outside one.

## Declined, and filed rather than dropped

`BooleanResultBase.RootValues` is the only one of the three root projections that is not cached —
`RootAssertions` and `AllRootAssertions` are both `field ??= ….ToArray()`, so every read of
`RootValues` re-runs the whole fold and discards its memo. The review is right that this fix makes
that walk load-bearing.

It is not fixed here. It is exactly as uncached before this change as after — the old walk re-ran per
read too — so it is not a regression this slice introduces, and turning a lazy `IEnumerable` into a
retained array is a memory and laziness trade that deserves its own measurement rather than riding
along in a correctness fix. Filed as [#193](https://github.com/karlssberg/Motiv/issues/193).

## Release note

`RootValues` now reports the metadata of **every** contributing operand when a higher-order result is
present in the subtree, where it previously reported only those reachable through the higher-order
side. Compositions with no higher-order result are unaffected at every node of the corpus.

The change is strictly additive per node — values are gained, never lost — so a consumer reading
`RootValues` as a set sees a superset. A consumer that had come to rely on the truncated answer was
relying on operands being dropped.

`Values`, `Assertions`, `Reason`, `Justification`, `MetadataTier.Metadata`, `MetadataTier.Underlying`,
`Explanation.Underlying`, `SubAssertions`, `RootAssertions` and `AllRootAssertions` are all unchanged.
