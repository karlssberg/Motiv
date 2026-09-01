# Spec 3A follow-up — the deepest tier is a property of a branch — Plan

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the residue left by the
[first 3A follow-up](2026-09-01-spec-3a-followup-metadata-sources.md) (PR
[#190](https://github.com/karlssberg/Motiv/pull/190), ticket
[#136](https://github.com/karlssberg/Motiv/issues/136)) and explicitly declined by the
[second](2026-09-01-spec-3a-followup-source-fallback.md) (PR
[#191](https://github.com/karlssberg/Motiv/pull/191), ticket
[#188](https://github.com/karlssberg/Motiv/issues/188)). Tracked as
[#189](https://github.com/karlssberg/Motiv/issues/189).

Not a build-map slice: #189 is a bug ticket spawned by 3A, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no row in that map's slice table —
the same call #136 and #188 made. It is recorded on #169 under the follow-ups the shipped slices
spawned.

## The debt being paid

`RootValues` is documented as *"the metadata yielded by all results that evaluated"*. On the corpus
node `greater than 2 & greater than 4 & (all neighbours == true)` it reported one of the three:

```
node.RootValues            == ["n != k"]
causal leaves of that node == ["greater than 2", "greater than 4", "n != k"]
```

`greater than 2` and `greater than 4` are causal operands of a satisfied `And`. They evaluated, they
contributed, and they were not reported.

#136 had already repaired 566 of the corpus's 13,680 nodes by fixing the source walk the tier tree is
built from. This is what that repair did not reach, and the boundary was exact: over the whole corpus
the invariant held at every node without a higher-order result in its subtree and failed only at nodes
with one.

## The decision

The ticket's stated suspect was `MetadataNode.Resolve`'s **collapse rule** — the rule that drops a
tier restating its children and splices their children in its place. That is where the values are
lost, and it is not what is wrong. The collapse is a deliberate, load-bearing contract, and the
verification that establishes this is in the design doc.

The fault is in the **walk**, not the tree. `GetRootValues` descended `MetadataNode.Underlying`, which
is lossy by design in two ways at once — it skips a level that merely restates its children, and it
returns a flat list with no branch identity. A branch whose deepest tier *is* the level that got
collapsed is therefore inexpressible in it. The existing `ElseIfEmpty` fallback hid this, because it
fires only when the *whole* level came out empty; it cannot tell a level that lost one operand from
one that kept them all.

So the walk now descends the un-collapsed direct children instead, and falls back per branch rather
than per level. `Underlying` is untouched.

## Explicitly out of scope

**`RootValues`' missing cache** ([#193](https://github.com/karlssberg/Motiv/issues/193)), raised by
the review pass. It is exactly as uncached before this change as after, and turning a lazy
`IEnumerable` into a retained array is a memory trade that wants its own measurement.

**The same defect in `RootAssertions`.** `Explanation` is the metadata tier's twin — same
`Resolution<T>`, same collapse, same `Underlying`. Measured the same way, `RootAssertions` disagrees
with an independent descent on **84 of the 13,680 nodes, every one of them in a higher-order subtree
and none outside it** — the identical boundary #189 had.

It is not fixed here. The two properties are not the same size of change: `RootAssertions` and
`AllRootAssertions` are named by 46 lines across the test suites and the example projects, against 15
for `RootValues`, and the assertion tree also feeds `SubAssertions` and every description formatter.
That belongs in its own review. Filed as [#192](https://github.com/karlssberg/Motiv/issues/192)
and recorded on #169.

## Steps

1. **Failing test first.** The acceptance test the ticket names: delete the higher-order exclusion
   from `UnderlyingMetadataSourcesTests.Should_reach_every_causal_leaf_from_RootValues`.
2. **Watch it fail for the right reason** — seed 78, reporting exactly the two operands the ticket
   names.
3. **Confirm the diagnosis against the tree** rather than against the ticket's prose: dump the failing
   node's tiers and establish that its level collapsed, that the two plain siblings are leaf tiers, and
   that the six surviving tiers are all from the higher-order side.
4. **Try the ticket's suspect and let it refute itself.** Splicing a childless child in as its own
   replacement fixes `RootValues` — and when the same rule is applied to the assertion twin it fails
   148 tests, because `Explanation.Underlying` being empty when nothing lies deeper is an explicit
   contract. The collapse is correct; the walk over it is not.
5. **Implement**: an internal `MetadataNode.Branches` — the un-collapsed direct children — and a
   root-values fold that descends it and falls back per branch.
6. **Keep the corpus's premise honest.** With the exclusion gone, the filter's own claim needs
   asserting separately, or a corpus that stopped generating higher-order results would leave the
   invariant green while covering none of what #189 was about.
7. **Full solution suite**, and a timing comparison against the pre-change walk, since `Branches`
   visits more nodes than `Underlying` did.
8. **`code-simplifier` pass**, per `CLAUDE.md` — which found a second instance of the same defect,
   one the corpus cannot reach. Written failing first, like the rest.

## Verification

- All thirteen test projects green on net10.0; `Motiv.Tests` also green on net8.0 and net9.0.
- `StackSafeTraversalOracleTests` green — the differential gate agrees at every node of every
  generated tree.
- `DeepCompositionTests` green, including the small-stack `RootValues` ceiling and the
  `SubAssertions`/`Explanation.Underlying`-are-empty contracts the rejected fix broke.
- The acceptance test fails before the change and passes after it, with no exclusion.
- **No test outside `UnderlyingMetadataSourcesTests` changed**, and within it only the exclusion was
  deleted — the two new tests are additions.
- Over the corpus, exactly **2 of 13,680 nodes** change, both in higher-order subtrees, both gaining
  values and none losing any.
- net472 is built but not run: no `mono` host on this machine, which is a standing local limitation
  rather than anything this change introduced. CI runs it.
