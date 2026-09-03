# Spec 3A follow-up — the same defect in the assertion tree — Design

**Date:** 2026-09-03
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1. Ticket [#192](https://github.com/karlssberg/Motiv/issues/192); the plan is
[here](../plans/2026-09-03-spec-3a-followup-root-assertions-branches.md).

Spec 3's §4 asks that *"every public result-tree property behaves identically at every depth"*.
`RootAssertions` did not, on the same bounded and exactly-characterised set of nodes its metadata twin
failed on, and had not since before 3A. This slice closes it, and is the last link in the chain 3A
started.

## What changed

`GetRootAssertions` stops descending `Explanation.Underlying`:

```csharp
-        var rootAssertions = FoldEach(result.Explanation.Underlying, ExplanationUnderlying, CombineRootAssertions)
-            .DistinctWithOrderPreserved()
-            .ElseIfEmpty(result.Assertions);
+        var rootAssertions = FoldEach(result.Explanation.ToEnumerable(), ExplanationBranches, CombineRootAssertions)
+            .DistinctWithOrderPreserved();
```

and descends a new internal `Explanation.Branches` — `CausalResolution.Children`, the un-collapsed
direct children — starting from the result's own explanation rather than from the level below it.

`CombineRootAssertions` is unchanged: it already fell back when a branch **contributed nothing**
rather than when it **had no children**, which is the form #189's review round had to correct in the
metadata fold *by copying it from here*. The whole of the fix is which tree the fold walks and where
it starts.

Three things follow from starting at the root rather than one level down:

- The trailing `ElseIfEmpty(result.Assertions)` is gone, because the root is now a branch like any
  other and falls back through the same rule. It was the level-wide fallback that hid the defect.
- `Explanation.Underlying`, `Explanation.AllUnderlying`, `SubAssertions`, `AllSubAssertions`, the
  collapse rule and every description formatter are untouched.
- `AllRootAssertions` is untouched, for the reason in [its own
  section](#the-half-of-the-ticket-that-was-already-correct).

`Branches` is the assertion twin of `MetadataNode.Branches`, and unlike it needs no leaf guard:
`Explanation.Causes` defaults to `[]` rather than to null, so a leaf explanation resolves to no
children of its own accord.

## What was inherited rather than re-derived

The diagnosis is #189's, and this slice deliberately spends none of its length re-deriving it. Its
design doc establishes, with a 148-test refutation, that `MetadataNode.Resolve`'s collapse rule is
where the operands are lost and is nevertheless correct; that `Underlying` is lossy twice over by
design; and that "the deepest level" is a property of a **branch** while `Underlying` is a property of
a **level**. All of it transfers verbatim, because `Explanation` and `MetadataNode` are the same tree
with the same `Resolution<T>` and the same collapse.

What this slice owed was not a second diagnosis but a second **measurement**, because a ticket that
inherits its reasoning can inherit a wrong premise just as easily as a right one. Before touching the
walk, the boundary was re-measured from scratch:

| Nodes | Mismatches with an independent causal-leaf descent |
|---|---|
| Subtree contains a higher-order result (2,063) | **84** |
| Subtree contains none (11,617) | **0** |
| Nodes reporting an assertion the oracle does not | **0** |

Both counts are exactly what #192 predicted. The third row is the one #189's measurement also led
with: the walk only ever under-reported, so the fix can only add.

## The half of the ticket that was already correct

#192 asks for `Branches` *"over `CausalResolution.Children` for `RootAssertions`, and over
`AllResolution.Children` for `AllRootAssertions`"*, and its acceptance test says `AllRootAssertions`
*"needs the same treatment against `Underlying`"*. Measured against an independent descent of
`BooleanResultBase.Underlying` to the leaves, `AllRootAssertions` disagrees on **0 of 13,680 nodes**,
before the change and after it.

It never had the defect, and the reason is structural rather than lucky. `GetAllRootAssertions` does
not walk the `Explanation` tree at all — it folds `BooleanResultBase.Underlying` directly, and
`CombineAllRootAssertions` iterates that list and falls back **per child** inside the loop:

```csharp
foreach (var underlying in result.Underlying)
{
    var fromUnderlying = foldedUnderlying[next++];
    rootAssertions.AddRange(fromUnderlying.Length == 0 ? AsArray(underlying.Assertions) : fromUnderlying);
}
```

That is already the per-branch granularity #189 had to introduce, arrived at independently, on a tree
that never collapses a level in the first place. The two siblings looked alike enough in the ticket's
prose — one line apart in `BooleanResultBase`, one letter apart in their names — for the defect to be
assumed shared.

The invariant is added anyway. It is not a fix; it is the guard that keeps the absence true, and it
costs one theory to state.

**A ticket's diagnosis is a hypothesis about code the ticket's author did not re-read.** #192 was
written from #189's understanding of the *tier* tree, and #189's understanding was correct — about
`RootAssertions`. Extending it one property sideways, to a walk with a different shape, is where it
stopped holding. The cheap defence is the one used here: measure each claim separately before
implementing any of them, so a wrong half is caught by arithmetic rather than by a reviewer.

## The oracle was pinning the defect

The change turned `StackSafeTraversalOracleTests` red on 14 of 150 seeds. That test is Spec 3A's
acceptance gate — every walk compared, at every node, against `RecursiveTraversalOracle`, a *verbatim
copy of the recursion Spec 3A replaced*. Its `GetRootAssertions` was that recursion, defect included:

```csharp
GetRootAssertions(result.Explanation.Underlying).DistinctWithOrderPreserved().ElseIfEmpty(result.Assertions)
```

So the gate was green for the whole of 3A while the property was wrong, and it went red the moment
the property became right. That is not a failure of the gate — it is exactly what a differential
oracle does, and the class already carries the precedent in its own remarks: `UnderlyingMetadataSources`
was settled as defective by #136, all three source walks lost their fallback-to-self at #188, and for
those the oracle's claim was explicitly weakened from *"does the fold match what shipped before Spec
3A?"* to *"does the fold match an independent recursive formulation?"*. `GetRootAssertions` now joins
them, and the remark says so.

Two things keep that weakening from being self-serving:

- The oracle stays **independent**. Its `ExplanationBranches` rebuilds an explanation's children from
  `Causes` and its own `UnderlyingAssertionSources` recursion, rather than reading
  `Explanation.Branches` — so it is a second formulation, not a transcription of the fold. Extracting
  it also let `ResolveUnderlying` be expressed as *branches, then the collapse*, which is what that
  method always was.
- The **behavioural** claim moves to a test that owes the `Explanation` tree nothing at all:
  `RootAssertionsBranchesTests` descends `BooleanResultBase.Causes` on the result tree and compares
  against `RootAssertions` at all 13,680 nodes. Without it, amending the oracle would leave a pair of
  mutually-consistent walks and no external check.

**An oracle transcribed from shipped code asserts that behaviour never changes, not that it is
correct.** Every such gate has a half-life: it is worth exactly as much as the audit of the code it
was copied from, and its green is not evidence past that point. Four of the oracle's twelve entries
have now been amended for defects the oracle itself was pinning.

## Blast radius, which was the ticket's stated reason for deferring

#189 deferred this on size: `RootAssertions` and `AllRootAssertions` are named by 46 lines across the
test suites and the example projects, against 15 for `RootValues`, and the assertion tree additionally
feeds `SubAssertions` and the description formatters. That estimate was right about the exposure and
wrong about the consequence — **no existing test changed except the oracle**.

The reason is the third row of the measurement table. The change is strictly additive per node, and
additive only where a higher-order result sits in the subtree beside a shallower sibling. None of the
46 lines asserts `RootAssertions` at such a node. The one example-project reference —
`DynamicPricingPolicyTests` — names it only in a Shouldly failure message, never in the assertion
itself, so it could not have caught this and cannot be broken by fixing it.

That is worth recording precisely because the deferral was still the right call. The blast radius was
unknown when #189 was written, and *"46 lines name it"* is the correct thing to say about an unknown
one. The cost of finding out was a second review; the cost of being wrong inside a correctness fix
would have been a wrong repair shipped green, which is the failure #189's own doc is about.

## The corner that exists in one twin and not the other

#189's `code-simplifier` round found a case the corpus cannot reach — a branch that *has* children but
whose whole subtree yields no metadata — and corrected the metadata fold to fall back on a branch
having **contributed nothing** rather than on it being **childless**, matching the form
`CombineRootAssertions` already had here. Writing the mirror of that test was the obvious next move,
and it fails:

```csharp
result.RootAssertions   // ["yields nothing == true", "sibling-true"]
result.RootValues       // ["all yielded nothing",    "sibling-true"]
```

Same tree, same node, different answers — and both correct. A proposition built as
`Create("yields nothing")` over `WhenTrueYield(_ => Enumerable.Empty<string>())` yields no metadata,
so in the tier tree that branch genuinely contributes nothing and the higher-order level above it *is*
the deepest. In the assertion tree it contributes `"yields nothing == true"`, because Motiv's
`== true` / `== false` suffix rule makes a supplied name the source of the assertion text and demotes
the yielded strings to `Values`. **Assertions have a total fallback; metadata has none.**

So the corner is unreachable here. Every proposition asserts something: a named one by the suffix
rule, an unnamed explanation one because `Create()` guards its `trueBecause` string as non-whitespace,
and a degenerate runtime string by falling back to `"statement == true"`. The two fallback forms —
flatten-first and childless-only — are therefore **indistinguishable in the assertion tree**, and no
test can separate them.

The flatten-first form is kept regardless, since it is what shipped and is the safe one of the two.
What the test became is the characterisation of the asymmetry: the two walks are pinned side by side
on one tree, asserting the divergence and naming the suffix rule as its cause. Anyone later "fixing"
one to agree with the other has to delete an explanation of why they differ.

**A twin's test does not always have a twin.** #189's doc closes on *"a generated corpus establishes a
boundary; it does not enumerate the shapes outside one"* — true, and the sibling lesson is that a
hand-written test for a shape outside the boundary is only meaningful where the shape exists. Copying
it across a symmetry without checking would have pinned a false claim, in a file whose whole purpose
is to hold a real one.

## What the review pass found

The `code-simplifier` round applied four edits, all clarity, and left one finding standing:

- The class-level remark on `AssertionExtensions` justified the walk-local memo by saying *"these four
  walks take an arbitrary sequence rather than a single result"*. `GetAllRootAssertions` already took
  a single result, and this change made `GetRootAssertions` take one too, so half the members
  contradicted the premise of the sentence explaining them. The memo is walk-local because these walks
  fold over nodes they do not own; that is now what it says.
- The oracle's fallback had been written `explanation.Assertions as string[] ?? …ToArray()`, importing
  the production `AsArray` trick into a class whose whole value is being an obviously-correct
  independent formulation — and handing a live array reference back out of the object under test.
  Plain `.ToArray()`.
- `Leaf`, `ContainsHigherOrder` and `DistinctInOrder` were verbatim in both twins' suites. They are
  context-free fixtures and a shape predicate, not the nuanced builder paths `CLAUDE.md`'s
  "avoid over-DRYing" rule protects, so they moved to `OracleHelpers`. `DistinctInOrder` stays a
  hand-written re-implementation there rather than delegating to Motiv's `DistinctWithOrderPreserved`:
  an expectation that borrows the production helper is one step less independent than it claims.
- The class summary said these invariants *"close #192"*, which is true of one of them. The
  `AllRootAssertions` invariant refutes the ticket's other half and guards the absence; the summary
  now says which is which.

**Declined:** consolidating the `yieldsNothing` fixture, which appears in both suites asserting
`RootValues` in one and both projections in the other. The duplication is the content — the whole
point of the assertion-side test is that the same tree gives two different answers — and merging the
fixtures would leave the contrast stated in one place and assumed in the other.

**The round also left a real question open, and it had a better answer than expected.** Its check on
whether the two hand-written tests were load-bearing was cut short. Run afterwards against the
pre-change production files, both go red: the corpus invariant on 14 of 150 seeds, and the
characterisation test too — the old walk reported `["all yielded nothing", "sibling-true"]` there,
collapsing past the operand that does assert. So the test that was written to *describe* an asymmetry
turns out to also *detect* the defect, from a single hand-built tree, where the corpus needed 13,680
nodes to find 84.

**The round is also the reason this section can be specific about its own process.** Its first act was
to `git checkout` the two production files to run exactly that mutation check — over the only copy,
which was unstaged. The check was right and the method destroyed the work; it survived only because
the same tool call had copied both files aside first. The lesson is the ordinary one and worth writing
down anyway: **a mutation experiment belongs on a copy of the tree, never on the tree**, and the
cheapest guard is to commit before inviting anything to mutate the working directory.

## Measurement

Over the Spec 3A corpus — 13,680 nodes across 150 seeds, comparing the new walk against the old one
rebuilt from the public `Explanation.Underlying` surface, which the fix leaves unchanged:

| | Count |
|---|---|
| Nodes whose `RootAssertions` changes | **84** |
| …of those, in a subtree containing a higher-order result | 84 |
| …in a subtree containing none | 0 |
| Nodes that **lose** an assertion | **0** |
| Nodes that gain one | 84 |

The zero is the row that matters, and it is the third time this chain has produced it — #136's repair
was *"strictly gaining values it had been dropping, none losing any"*, #189's was the same, and so is
this. A walk that was under-reporting can only be repaired upward.

## Cost

`Branches` never collapses, so the walk visits more nodes than `Underlying` did. Measured on `And`
chains, best of five per size, in Release on net9.0:

| Operands | Before | After |
|---|---|---|
| 500 | 1 ms | 1 ms |
| 1,000 | 11 ms | 5 ms |
| 2,000 | 48 ms | 41 ms |
| 4,000 | 174 ms | 122 ms |

No slower, and consistently a little faster. The extra nodes are more than paid for by folding once
from the root with one memo, where the old form re-entered a lazy `ElseIfEmpty` per level and rebuilt
its iterator chain each time. As with #189, the tier structure's own edge count dominates and the walk
over it does not. `DeepCompositionTests.Should_read_RootAssertions_of_a_deep_composition` passes on
its 1 MB thread, unchanged.

## Release note

`RootAssertions` now reports the assertions of **every** contributing operand when a higher-order
result is present in the subtree, where it previously reported only those reachable through the
higher-order side. Compositions with no higher-order result are unaffected at every node of the
corpus.

The change is strictly additive per node — assertions are gained, never lost — so a consumer reading
`RootAssertions` as a set sees a superset. A consumer that had come to rely on the truncated answer
was relying on operands being dropped.

`Assertions`, `AllAssertions`, `SubAssertions`, `AllSubAssertions`, `AllRootAssertions`, `Reason`,
`Justification`, `Explanation.Underlying`, `Explanation.AllUnderlying`, `Values` and `RootValues` are
all unchanged.

## The chain, closed

3A deferred the parent-vs-child divergence to [#136](https://github.com/karlssberg/Motiv/issues/136);
#136 deferred the fallback-to-self to [#188](https://github.com/karlssberg/Motiv/issues/188); #188
declined `RootValues`' own fallback and named [#189](https://github.com/karlssberg/Motiv/issues/189);
#189 ends by naming #192. This is that node, and it names nothing further — the correctness chain
stops here. [#193](https://github.com/karlssberg/Motiv/issues/193), the missing cache on `RootValues`,
remains open and is a cost question rather than a correctness one, as
[#137](https://github.com/karlssberg/Motiv/issues/137) and
[#195](https://github.com/karlssberg/Motiv/issues/195) were.
