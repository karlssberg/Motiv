# Spec 3A follow-up — the cost that was a cycle — Design

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1. Ticket [#137](https://github.com/karlssberg/Motiv/issues/137); the plan is
[here](../plans/2026-09-01-spec-3a-followup-metadata-tier-cost.md).

Spec 3A measured two costs it could not fix and filed them: `RootValues` over a fully-causal chain
grew as roughly `n^2.6`, and a chain of 300 identically-named propositions ran out of memory. Both
were already gone by the time this slice opened the ticket. This slice establishes that with
evidence, holds it with tests that fail against the code that had the defect, and brings home the
regression case 3A had to park elsewhere because of it.

## What changed

**No production code.** The library is byte-identical. What ships is:

- `src/Motiv.Tests/Traversal/MetadataTierCostTests.cs` — six cases, every one of which
  fails against `08f81f5c`.
- `DeepCompositionTests.Should_read_RootValues_of_a_deep_composition` moves from `DeepOrElse()` to
  `DeepAnd()`, which is the ticket's explicit ask, with its `<remarks>` rewritten.

A slice with no diff in `src/Motiv` is an unusual thing to ship, and it is the right shape here: the
defect is fixed, nothing held it fixed, and the ticket cannot be closed on a reading of the diffs
that fixed it. It is closed on a measurement.

## The ticket named the right place and the wrong cause, again

#137 suspected `MetadataNode.Resolve`'s collapse comparison:

> `MetadataNode.Underlying` resolves its children by computing every child tier's metadata and
> comparing the distinct union against its own. Over a fully-causal chain, a node at level *k* has
> O(k) child tiers, and each of those carries O(k) metadata, so the comparison alone is O(k²) per
> level.

The arithmetic is exact and the conclusion is right, and the collapse rule is not what put O(k)
metadata on each of those children. Until [#136](https://github.com/karlssberg/Motiv/issues/136),
`UnderlyingMetadataSources` reported a composition as **its own source** — it added `result` once per
non-operation child rather than the child the walk stopped at. `Resolve` consumes that property
directly, so for level *k* of an `And` chain the children were not *k* leaf tiers carrying one
metadatum each; they were the node itself plus its *k−1* operation-result descendants, each carrying
*k* metadata.

That single wrong edge accounts for both findings at once:

| Finding | What the self-edge does |
|---|---|
| `n^2.6` rather than `n²` | the metadata `Resolve` must union at level *k* is O(k²) instead of O(k) — one extra factor of *n* over the chain |
| Out of memory | the child set contains the node, so "the children say exactly what I say" is trivially true, the level collapses into itself, and the descent has a cycle to follow |

This is #189's shape exactly — *"the ticket named the wrong suspect, and proving it is the design"* —
and it is worth noticing that it happened twice in the same tree. Both tickets pointed at `Resolve`
because `Resolve` is where the symptom is observable. Neither defect was in it.

## Measurement

Left-deep `And` chain, every operand causal and distinctly named, 1 MB thread, Release, net10.0.
`08f81f5c` is the commit before #136's repair; `790d18b6` is `main`.

| Operands | `RootValues` at `08f81f5c` | `RootValues` at `790d18b6` | `Values` at `790d18b6` |
|---|---|---|---|
| 500 | 514 ms | 9 ms | 5 ms |
| 1,000 | 3,128 ms | 34 ms | 27 ms |
| 1,500 | 10,354 ms | 102 ms | 55 ms |
| 2,000 | 25,333 ms | 142 ms | 206 ms |
| 3,000 | 85,908 ms | 311 ms | 266 ms |

Fitted over the 500→3,000 range the exponent falls from **2.85 to 1.97**. The ticket's own figures
(46 s at 2,000, 163 s at 3,000) are roughly double the first column, which is a machine difference and
not a disagreement: fitted the same way its table gives 2.84, so it is the same exponent. The ticket
rounds that to `n^2.6` in its prose; its own numbers say 2.84, and the prose is what a reader
remembers, so it is worth saying which one to trust.

The third column is the one that settles what is left. **`RootValues` now costs about what `Values`
costs**, and `Values` touches no walk at all — it materialises the tier. The walk is no longer the
expensive part of reading it.

## The out-of-memory, and the shapes the ticket did not name

At 300 operands with a 512 MB heap cap, all identically named:

| Chain | `08f81f5c` | `790d18b6` |
|---|---|---|
| `And` | OOM after 2.3 s | 0 ms, `["is even == true"]` |
| `Or` | OOM after 1.5 s | 0 ms |
| `XOr` | OOM after 1.6 s | 0 ms |
| `AndAlso` | OOM after 1.5 s | 0 ms |
| `OrElse` | 0 ms | 0 ms |

The ticket named `And`. It is every combinator that leaves each operand of the chain causal — which
is what one expects once the cause is a self-edge, since the self-edge is added per non-operation
causal child and has nothing to do with which operator produced it. `OrElse` is the exception for the
same reason it was 3A's escape hatch: it leaves one causal operand per level, so there is no second
child to trigger it.

Each of the ten combinator-by-naming combinations was run twice, once with atomic operands and once
with a higher-order proposition as the operand — twenty runs, since a higher-order result is the node
type #189 turned on. It makes no difference to either column, which is itself worth recording: this
defect is upstream of the branch-identity problem #189 fixed, and independent of it.

## The regression cover, and how it was proved red

The fix shipped three commits ago, so "write the failing test first" has only one honest form here:
check `src/Motiv` out at `08f81f5c`, run the new cases against it, and record how each fails.

| Case | Failure at `08f81f5c` |
|---|---|
| `Should_read_RootValues_of_a_chain_of_identically_named_propositions` (×4 combinators) | `OutOfMemoryException` |
| `Should_not_report_any_composition_in_the_chain_as_its_own_metadata_source` | the self-edge is present at every level |
| `Should_reach_operands_carrying_one_value_each_rather_than_compositions` | sum is **45,151**, expected 300 |

45,151 is `300 × 301 / 2 + 1`. That is not a number anyone reasoned to; it is the quadratic metadata
union falling out of the assertion, and it is the closest this slice gets to observing the extra
factor directly.

**The cost case is stated structurally rather than on a clock.** A source is an operand, so it carries
the one value that operand yielded, and the metadata `Resolve` unions at the root is therefore linear
in the chain. While a composition was its own source that sum was quadratic. Asserting the sum is
`Operands` fixes the exponent without a stopwatch — which matters because CI runs Windows and a
timing assertion there is a flake waiting to be re-run rather than read.

**The mechanism is asserted separately from the symptom.** Without
`Should_not_report_any_composition_in_the_chain_as_its_own_metadata_source`, a regression reports
`OutOfMemoryException` from inside a 300-deep composition, which says nothing about where it came
from. With it, a regression names the cyclic edge.
`UnderlyingMetadataSourcesTests.Should_yield_the_child_the_walk_stopped_at_rather_than_the_result_itself`
already makes that claim over three operands; the difference is depth, and depth is the whole
finding — at three operands the self-edge is a wrong answer, at three hundred it is fatal.

## The case comes home

3A's remark on `Should_read_RootValues_of_a_deep_composition` said it ran on the short-circuiting
chain *"because the metadata tier over a fully-causal `And` chain has a quadratic-plus number of
edges — a cost that predates this slice and is not this slice's to fix"*. That is no longer true, and
a stale reason in a test is worse than none, because it reads as a live constraint. The case now runs
on `DeepAnd()` at 3,000 operands like every other case in the file, and its remark records what
happened instead.

**`Should_read_every_member_of_one_deep_result` deliberately stays on `DeepOrElse()`.** It is the
uniformity invariant — no public member has a lower depth ceiling than another — and this chain is
satisfied at its first operand, so every level of it is the *single-operand* `OrElse` node, a result
shape the `And` chain does not contain at all. Moving it would trade that coverage away for nothing.

It is emphatically **not** a cost decision, and the review pass is what made that checkable: it
measured the case on `DeepAnd()` at 890 ms. Recorded in the file as well as here, because moving one
of the two cases and not the other otherwise reads as an oversight — which is exactly what the review
pass reported when it saw the diff.

## What remains, and whose it is

A clean quadratic, and it is not `RootValues`'.

`MaterialiseMetadataTiers` folds the whole result tree bottom-up and touches every node's
`MetadataTier.Metadata` — 3A's first unlisted finding, and the reason `Values` no longer overflows the
stack on a deep chain. Level *k* of the chain holds *k* distinct values, so materialising all *n*
levels materialises Θ(n²) `(level, metadatum)` pairs. Reading the root's `Values` — an answer with *n*
items — pays for every level's set on the way, which is what the third column of the table above is
showing.

Whether that is a defect at all is a real question. `MetadataNode.Metadata` is public and per-node,
and each level genuinely carries a different set, so Θ(n²) may simply be the size of what the API
exposes rather than overhead on top of it. Making a root read linear means not materialising the
intermediate levels, which is a change to the tier's laziness contract — and would reintroduce the
overflow 3A fixed unless the lazy union is restructured at the same time.

Not answered here, and not dropped either: filed as
[#195](https://github.com/karlssberg/Motiv/issues/195) with the measurement, the mechanism, and the
three wrong turns to avoid. It sits next to [#193](https://github.com/karlssberg/Motiv/issues/193),
which asks the adjacent laziness question about `RootValues`' missing cache.

## What 3A's design doc still says

> `RootValues` over a fully-causal chain is quadratic-plus in the metadata tier, and a chain of
> *identically-named* propositions runs out of memory at 300 operands.

Left as written. It is a dated record of what that slice measured, it was true when measured, and
#136 and #189 both left it alone for the same reason. The ledger's follow-up table and this document
carry where it was settled. Rewriting a shipped design doc to match a later repair would make the
series unreadable in the one way it is currently readable — each doc says what was true when it
shipped.

## Release note

None. No public behaviour changes, and no production code is touched.

## What the review pass found

Four edits, all applied, and one rejected reason for an edit that was applied anyway.

**The small-stack wrapper was inert, and inert rigging is a false claim.** The new cases were written
inside a copy of `DeepCompositionTests`' `OnASmallStack` — a 1 MB thread — by reflex, because that is
what the neighbouring class does. The review pointed out it can change nothing here: since 3A the tier
walk is a heap fold rather than recursion, which is precisely *why* the pre-#136 failure is an
`OutOfMemoryException` and not a `StackOverflowException`, and 300 is an order of magnitude short of
the depth at which `DeepCompositionTests`' 1 MB budget is the claim being made. A test rig that
implies a ceiling the test does not measure is the small version of the lesson #169 records from 4I —
*a check reporting a property it is not actually checking is worse than no check*. Deleted, not
extracted into a shared helper: extracting would have pulled `DeepEvaluationTests`, untouched by this
slice, into the diff to solve a duplication that is load-bearing in both places it remains.

The review attached a condition to its own finding — that the six cases be re-proved red against
`08f81f5c` *without* the wrapper, because it had only verified green on `main`. That was the right
condition and it was met: same six failures, same three modes, 45,151 again.

**The exponent was attributed to a slice that never measured it.** The class summary said 3A had
measured `n^2.85`. 3A filed `n^2.6`; `2.85` is this slice's own re-measurement on a different machine.
Both numbers are now in the summary, each with its owner.

**`ArgumentOutOfRangeException` said "not a combinator".** `OrElse` is a combinator — it is a
deliberately excluded one, and the theory's summary says why. Anyone adding `[InlineData("OrElse")]`
to see what happened would have been told something false by the failure.

**The class was renamed** from `RepeatedMetadataCompositionTests` to `MetadataTierCostTests`. Only one
of its three tests is about repeated metadata; the other two build distinctly-named chains and are
about the tier's shape.

**The reason offered for the `DeepOrElse()` remark was wrong, and the remark still needed writing.**
The review proposed explaining the non-move as a cost decision — and had itself measured 890 ms on
`DeepAnd()`, which refutes that. The remark shipped with the real reason: the short-circuited chain
carries a node shape the `And` chain does not have. The finding was right and its reasoning was not,
which is worth separating rather than accepting wholesale.
