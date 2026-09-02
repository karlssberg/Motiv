# Spec 3A follow-up — the square that was a root read — Design

**Date:** 2026-09-02
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1. Ticket [#195](https://github.com/karlssberg/Motiv/issues/195); the plan is
[here](../plans/2026-09-02-spec-3a-followup-tier-materialisation.md).

Spec 3A left two costs behind. [#136](https://github.com/karlssberg/Motiv/issues/136) removed the
extra factor and [#137](https://github.com/karlssberg/Motiv/issues/137) proved it removed, leaving a
clean square that #137 filed rather than fixed — and filed *with the case for leaving it alone*. This
slice answers that case and removes the square from the read it did not belong to.

## The ticket argues against itself, and the argument is half right

#195 states the reason it might not be a defect at all:

> `MetadataNode.Metadata` is public and per-node, and each level genuinely does carry a different set.
> Θ(n²) is then the size of what the API exposes, not overhead on top of it: no walk over that
> structure can be cheaper than the structure.

Every clause of that is true, and the conclusion does not follow, because the argument never says
**who is asking**. Θ(n²) is the size of the answer to *"give me every level's set"*. The answer to
*"give me the root's"* has *n* items. `Values` was charging the second question the price of the
first, and the ticket's own table is what shows it: `RootValues` had fallen to roughly what `Values`
costs, and `Values` performs no walk at all.

Both halves survive in the shipped code, and the measurement below shows both: a root read is now
linear, and reading every level of the same chain still materialises 4,501,499 `(level, metadatum)`
pairs at 3,000 operands. **The square did not go away. It stopped being charged to a caller who asked
for one level.**

## What changed

| File | Change |
|---|---|
| `src/Motiv/Shared/MetadataNode.cs` | an internal constructor for a node that is the union of its causes', and an iterative `UnionOf` that computes it by descending past the levels that only union what is beneath them |
| `src/Motiv/BooleanResultBase.cs` | `MaterialiseMetadataTiers` now materialises a tier's *own* metadata only, passing over the union nodes |
| `BinaryBooleanResult`, `OrElsePolicyResult`, `AndAlsoPolicyResult` | the three tiers that were unions, now built as unions |
| `src/Motiv.Tests/Traversal/MetadataTierMaterialisationTests.cs` | six cases: four fail against the pre-change tree, one is the invariant they must not buy their bound with, one holds what the narrowed pass still does |
| `src/Motiv.Tests/Traversal/ChainSpine.cs` | the left-spine walk both cost files were writing out |

Before, a composition's tier held a lazy `causes.GetValues()` — a `SelectMany` over its causes'
`Values`, which are their tiers' *materialised sets*. Forcing the root's therefore required every
level's, which is where the square came from. Now the node holds its causes and unions the levels that
actually carry metadata, in one set, in one pass.

The old source is still there and still read, by `Resolve`, unchanged — see below.

## The three constructors, and why the union needed its own

`MetadataNode` had two public constructors and one internal leaf one. This adds a fourth, and the
reason is that the union was previously expressed *at the call site* rather than in the type: three
results each wrote `new(causes.GetValues(), causes)`, passing the same collection twice in two
different disguises — once as "here is my metadata", once as "here are my causes". Nothing in the node
knew those were the same thing, so nothing in the node could exploit it.

Naming the shape is the whole fix. Once the node knows its metadata *is* its causes' union, it can
compute that union any way it likes, and the way it likes is a single set filled by one descent rather
than *n* sets filled by *n* nested unions.

Every other tier in the library carries metadata of its own. That is not an assumption — it is the
result of reading all 55 construction sites, and it is why the blast radius is three files.

## Why `Resolve` still reads the lazy source

`Resolve` compares `children.SelectMany(n => n.Metadata).DistinctWithOrderPreserved()` against the
node's own `_metadataSource`, and collapses the level when they agree. The union node keeps that
source, undeduplicated, exactly as before.

Handing `Resolve` the computed set instead would have been the obvious tidy-up and is a behaviour
change. The source is a *concatenation* of the causes' sets, so an `And` of two identically-named
propositions gives it two entries where the distinct child union gives one; the `SequenceEqual` fails,
and the level does not collapse. Deduplicate it and the level starts collapsing. #189's design doc
holds the collapse rule with 148 tests, and #137 named reopening it as the first thing not to do —
this slice does not, and the distinction is subtle enough to be worth writing down.

## The bottom-up pass was still load-bearing, and its own comment said why wrongly

`MaterialiseMetadataTiers` is kept. The temptation to delete it as newly-redundant is a trap this
slice walked into, and the way out is the more interesting half of the work.

It looks redundant, and the evidence for that is strong: switch its forcing off and all 5,620 tests
pass; delete the fold outright and all 5,620 still pass. The review pass reached the same reading
independently and put the question plainly — either it is vestigial, or there is a shape that needs it
and it deserves a test.

It is the second, and the instrument that shows it is not a test-count but the stack depth at which
the *deepest* tier is actually constructed. A chain of decorators, read at the root:

| Decorators | pass on | pass off |
|---|---|---|
| 2,000 | 8 | 4,003 |
| 20,000 | 7 | 40,002 |

Two frames per level, and constant with the pass. Nothing in the suite could see it because a
decorator chain deep enough to overflow `DeepCompositionTests`' 1 MB thread exhausts that thread during
**evaluation** first, and the probes that missed it had been given 64 MB — enough rope for 200,000
frames, which is how "no overflow" got mistaken for "no recursion".

**The mechanism is the opposite of what the code claimed.** The remark this slice first wrote said a
tier's own source "may come from a lazy source that reads its causes' values — a decorator's, for one".
A decorator's is `predicateResult.Values`, and it is read **eagerly**, in the constructor. Eagerness is
precisely the problem: constructing the root's tier first constructs every tier beneath it, in one
nested chain. What the pass does is *construct* them deepest-first, so each of those eager reads finds
an answer already waiting.

So the pass has two halves, and only one was ever the union's:

1. **Touching `MetadataTier` bottom-up**, which constructs it. Load-bearing, measured above, and
   untouched by this slice.
2. **Forcing each tier's `Metadata`.** This is what the composition tiers needed, because their source
   was a lazy union that only recursed when enumerated — and it is what made a root read quadratic.
   It now skips them.

Half 2 is kept for the tiers that do carry their own metadata. It is not what the table above measures,
and this document does not claim it is: some own sources are lazy in the way the unions were —
`MinimalHigherOrderFromExpressionTreeBooleanResult`'s is a `SelectMany` over its causes'
`MetadataTier.Metadata` — and materialising deepest-first is the conservative preservation of what
those got before. It costs nothing beyond building a set that a read would build anyway.

`Should_build_the_deepest_tier_at_a_constant_stack_depth_however_long_the_chain` now holds half 1. It
is the only case in 5,621 that fails when the pass is removed, which is the same thing as saying it was
the gap.

## The cost, as a number rather than a duration

#195 asked for a structural bound, not a clock, and pointed at #137's
`Should_reach_operands_carrying_one_value_each_rather_than_compositions` as the form. The form used
here is a metadatum that counts the hashes taken of it: `HashSet<T>` hashes each item once as it is
added, so the count of hashes taken to answer one read is a **census of how many set memberships that
read built**. It is exact, deterministic on any runner, and needs no stopwatch — which matters because
CI runs Windows.

Left-deep chain, 300 operands, every operand causal and distinctly valued:

| Read | Before | After |
|---|---|---|
| root `Values` | 45,449 hashes | 600 |
| root `RootValues`, 300 operands | 46,049 | 1,499 |
| root `RootValues`, 600 operands | 182,099 | 2,999 |

45,449 is `n(n+1)/2` plus the walk's own handful — the same `300 × 301 / 2` that #137 met from the
other side, which is what a per-level set costs when there are *n* levels. 600 is `2n`: each value
hashed once into its operand's own set and once into the root's union. And the `RootValues` pair is
the exponent stated without a clock — doubling the chain took the old read from 46,049 to 182,099, a
factor of **3.95**, and the new one from 1,499 to 2,999, a factor of **2.00**.

The wall clock agrees, for whatever an unloaded MacBook is worth. Release, net10.0:

| Operands | `Values` before | `Values` after | `RootValues` before | `RootValues` after | every level, after |
|---|---|---|---|---|---|
| 500 | 5 ms | 3 ms | 9 ms | 5 ms | 39 ms |
| 1,000 | 36 ms | 2 ms | 40 ms | 9 ms | 171 ms |
| 2,000 | 103 ms | 1 ms | 188 ms | 17 ms | 217 ms |
| 3,000 | 229 ms | 1 ms | 262 ms | 31 ms | 487 ms |

The last column is the point of the whole slice. Reading all 3,000 levels still materialises
4,501,499 pairs and still takes about what it took before. Nothing was made cheaper. One read stopped
paying for the others.

## The case the fix must not buy its bound with

A "fix" that made every level report the root's set would pass every cost case above and be wrong, so
the second half of the cover asserts the structure the ticket defends: level *k* of the chain carries
exactly *k* values, checked at all 299 levels.

That case **passes against the pre-change tree**, and is supposed to. It is not regression cover for a
defect; it is the invariant the cost case is not allowed to break, and a slice whose new tests all fail
before and pass after has usually not written this one.

## How the cases were proved red

Production reverted to `HEAD`, cases run, production restored:

| Case | Failure before |
|---|---|
| `Should_hash_each_value_twice_when_only_the_root_is_read` (`And`) | 45,449, expected 600 |
| same (`AndAlso`) | 45,449 |
| same (`OrElse`) | 45,449 |
| `Should_scale_linearly_with_the_chain_when_reading_RootValues` | 182,099 for twice the chain that cost 46,049 |
| `Should_still_carry_a_distinct_set_at_every_level_of_the_chain` | passes, by design |

`Should_build_the_deepest_tier_at_a_constant_stack_depth_however_long_the_chain` arrived later, out of
the review round, and is proved against a different tree: it passes before *and* after this slice, and
fails only when the pass it describes is removed. That is the right control for a case that holds
something the slice narrowed rather than something it changed.

All three combinators reading the identical 45,449 is worth a second look: they build their tiers at
three different call sites, and the number is the same because the shape is. `OrElse` reaches it only
because the chain is run against an *unsatisfied* model, so it never short-circuits — the same
observation #137 made from the opposite side, where `OrElse` was the one chain that escaped its defect
by always short-circuiting.

## What the tests could not run

`Motiv.Tests` targets `net472` among others. It **compiles** for that framework — the first
full-solution run failed there and on `net8.0`/`net9.0` with `CS8130`, because `array.Reverse()` binds
to the void-returning span overload below `net10.0` and the `net10.0`-only run had not seen it — but it
cannot be *hosted* on macOS, where vstest needs `mono`. That abort is pre-existing and unrelated;
CI runs it.

Everything else is green: `Motiv.Tests` 5,621 × (`net8.0`, `net9.0`, `net10.0`), plus
`Motiv.Serialization` 980 × 3, `Motiv.Serialization.AspNetCore` 162, `Motiv.Studio` 109,
`Motiv.CodeFix` 94, `Motiv.Serialization.Sql` 71, `Motiv.Poker` 60, `Motiv.RuleAuthoring.Blazor` 49,
`Motiv.Serialization.EntityFrameworkCore` 43, `Motiv.Analyzer` 20, `Motiv.ECommerce` 11,
`Motiv.SmartHome` 8 and `Motiv.EntityFramework` 3.

`DeepCompositionTests.Should_read_Values_of_a_deep_composition` and its `RootValues` twin are the ones
that matter most: 3,000 operands on a 1 MB thread, both green, which is the direct evidence that
restructuring the laziness did not reintroduce the overflow 3A fixed — the specific hazard #195 warned
about.

## Release note

None. No public behaviour changes: the same values in the same order from the same members. What
changed is when the intermediate levels are computed.

## What remains

[#193](https://github.com/karlssberg/Motiv/issues/193) — `RootValues` is the only root projection
without a cache — is untouched and now more visible, since a root read that is linear is a read worth
not repeating. [#192](https://github.com/karlssberg/Motiv/issues/192), the assertion-side twin of
#136, is also still open.

## What the review pass found

Four applied, two rejected with reasons, and one question that turned into the slice's best measurement.

**The question was whether the bottom-up pass is now vestigial**, since removing it leaves every test
green. That is the section above: it is not vestigial, the suite could not see it, and the case that
now holds it exists because the review asked. The review's framing was exactly right — *either it is
vestigial, in which case delete it, or there is a shape that needs it and it deserves a test* — and
neither half could be settled by running the suite, which is what made it worth chasing.

**Three arms of `UnionOf`'s switch collapse to two.** "Already materialised" and "carries its own" are
the same operation, because `Metadata`'s `??=` returns the materialised set for the first. The rule now
reads as the sentence it implements: descend past an unmaterialised union, otherwise take what the node
has.

**`Push` allocated an array it did not need.** `causes.Select(c => c.MetadataTier).AsList()` can never
hit `AsList`'s no-copy path — a `Select` iterator is not an `IReadOnlyList` — whereas `causes.AsList()`
usually can, since every caller passes an array. Renamed `PushTiersOf`, and the backwards loop now says
why it counts down.

**Two invariants were true and nowhere written.** `_causes` and `_unionOfCauses` hold the same
reference and are read for unrelated questions; and `_metadataSource` on a union node is now read
*only* by `Resolve`, so a reader who deletes it as dead would silently change which levels `Underlying`
shows. Both are commented at the field.

**Collapsing `_causes` and `_unionOfCauses` to one field plus a `bool` was rejected**, and the reason is
worth keeping: a `bool` does not flow into nullable analysis, so both readers would acquire a `_causes!`.
Eight bytes against two null-forgiving operators on a read path is the wrong trade.

**`Should_scale_linearly_...` claimed an exponent from two points.** It does not fit an exponent and no
longer says it does; what it discriminates is doubling from quadrupling, at a threshold between them.
The name still says "linearly" — that is what the case is *about*, and the summary now says what it
measures.

**`Spine` was extracted; `Chain` and `Combine` were left duplicated.** The spine walk is a pure generic
lift and was byte-identical in both cost files, so it moved to `ChainSpine`. `Chain`/`Combine` differ in
metadata type and evaluation model, and `CLAUDE.md` is explicit about preferring that duplication to a
branching abstraction. The review recommended exactly this split.

**The review also caught the working tree mid-experiment**, with `MaterialiseOwnMetadata` reduced to
`if (false && …)` — the one form C# exempts from the unreachable-code warning, so it built silently
clean. That was this session probing whether the guard was inert, and it is recorded here because the
review was right to report rather than "fix" it: a guard that is a no-op with nothing saying so is the
failure mode the section above is about.
