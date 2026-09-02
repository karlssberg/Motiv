# Spec 3A follow-up — the square that was a root read — Plan

**Date:** 2026-09-02
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the last of the costs Spec 3A ([PR #134](https://github.com/karlssberg/Motiv/pull/134))
measured and deferred. Tracked as [#195](https://github.com/karlssberg/Motiv/issues/195), filed by
[#137](https://github.com/karlssberg/Motiv/issues/137) as the residual it deliberately left behind.

Not a build-map slice: #195 is a bug ticket spawned by 3A, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no row in that map's slice table —
the same call #136, #188, #189 and #137 made. It is recorded on #169 under the follow-ups the shipped
slices spawned.

## The debt being paid

The 3A cost thread has been unwound one factor at a time:

| Ticket | What it removed | What was left |
|---|---|---|
| [#136](https://github.com/karlssberg/Motiv/issues/136) | a composition reporting itself as its own metadata source | `n^2.85` → `n^1.97` |
| [#137](https://github.com/karlssberg/Motiv/issues/137) | nothing — it measured, and held the repair with six cases | a clean square |
| **#195** | the square, **for a caller who reads one level** | the square, for a caller who reads them all |

#137's closing measurement is the whole brief: `RootValues` now costs about what `Values` costs, and
`Values` touches no walk at all. The remaining cost is not in any traversal. It is
`MaterialiseMetadataTiers` folding the entire result tree and touching every node's
`MetadataTier.Metadata`, because a composition's tier took its metadata from a lazy union over its
causes' tiers. Level *k* of a chain holds *k* distinct values, so materialising all *n* levels
materialises Θ(n²) `(level, metadatum)` pairs — to answer a read whose answer has *n* items.

## The question the ticket actually asks

#195 does not ask for an optimisation. It asks whether there is a defect at all, and states the case
for "no" carefully enough that it has to be answered rather than waved past:

> `MetadataNode.Metadata` is public and per-node, and each level genuinely does carry a different set.
> Θ(n²) is then the size of what the API exposes, not overhead on top of it: no walk over that
> structure can be cheaper than the structure.

That is right about the structure and wrong about the read. The distinction the argument misses is
**who is asking**. Θ(n²) is the size of the answer to *"give me every level's set"*. It is not the
size of the answer to *"give me the root's"*, which is *n*. The defect is that the second question was
charged the price of the first.

So the deliverable is a **laziness** change, not a walk optimisation — exactly as the ticket predicts,
including its warning that it "would reintroduce the stack-overflow 3A fixed unless the lazy union is
restructured at the same time." Restructuring the lazy union is therefore the work, not a hazard to
route around.

## The decision

**Move the union into `MetadataNode` and make it iterative.** A composition's tier gets a constructor
saying what it is — a node carrying no metadata of its own, whose metadata is the union of its causes'
— and computes that union by descending past the levels that only union what is beneath them, into
the levels that do carry metadata. The descent is a loop, so it needs no bottom-up pre-pass to stay
off the stack.

`MaterialiseMetadataTiers` then keeps its job and loses one case: it still materialises every tier's
*own* metadata bottom-up, because a tier's own source may itself read its causes' values (a
decorator's does), and that is what would nest a frame per level. It passes over the union nodes,
which are the ones that were quadratic.

Three call sites construct a union tier — `BinaryBooleanResult`, `OrElsePolicyResult`,
`AndAlsoPolicyResult`. Every other tier in the library carries its own metadata, so the blast radius is
those three and the two files above.

## Explicitly out of scope

**`MetadataNode.Resolve`, `Underlying` and the collapse rule.** #189 established the collapse is a
load-bearing contract with a 148-test refutation of the alternative, and #137 restated it as the first
thing not to reopen. `Resolve` keeps reading the same lazy `_metadataSource` it reads today, so its
collapse comparison is untouched — deduplicating that source before the comparison would change which
levels collapse.

**[#193](https://github.com/karlssberg/Motiv/issues/193) — `RootValues`' missing cache.** Adjacent,
named by #195 as adjacent, and a different question: this slice changes what a read costs the first
time, not how many times it is paid.

**A wall-clock assertion.** CI runs Windows and this repo has been bitten by timing-sensitive tests
before. #195 says so explicitly, and points at
`MetadataTierCostTests.Should_reach_operands_carrying_one_value_each_rather_than_compositions` as the
form to copy: a structural statement.

## Steps

1. **Reproduce the square as a count, not a duration.** Give the chain a metadata type that counts its
   own `GetHashCode` calls: `HashSet<T>` hashes each item once as it is added, so the count is a
   census of how many set memberships a read built. This is the structural form the ticket asks for,
   and it is deterministic on any CI runner.
2. **Write the failing cases first** — the cost, over each of the three places a union tier is built.
3. **Write the case the fix must not buy its bound with**: every level still carries its own distinct
   set. A "fix" that flattens the tier would pass step 2 and be wrong.
4. **Implement** the union constructor, the iterative descent, and the narrowed materialisation.
5. **Prove the new cases red** against the pre-change tree, and record how each fails.
6. **Full solution suite**, all target frameworks — not just `net10.0`. The example projects assert on
   justification strings, and `Motiv.Tests` builds for `net472`.
7. **`code-simplifier` pass** per `CLAUDE.md`.

## Verification

- Every new case fails against the pre-change tree and passes after it, with the failure modes
  recorded.
- Reading the root of a fully-causal chain is linear in the chain; reading every level of it is still
  quadratic, because that is the size of the answer.
- Full solution suite green, with any suite that could not run named and why.
