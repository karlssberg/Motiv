# Spec 3B follow-up — the two renderers of one tree, and the run only one of them collapsed — Design

**Date:** 2026-09-03
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 2 — the decision log. Ticket [#139](https://github.com/karlssberg/Motiv/issues/139), filed
while building Spec 3B. Plan: [here](../plans/2026-09-03-spec-3b-followup-reason-run-collapse.md).

The first follow-up in this series that is not 3A's. #136 through #193 all came out of Spec 3A's
traversal work and all concerned the *result* tree; this one concerns the **description** tree, which
3A did not touch — `DescriptionBaselineTests` exists precisely because that tree's twelve bespoke
formatters could not be folded onto the traversal the way the result-tree walks were.

**It does not close its ticket.** See "What this leaves open".

## What changed

`Reason` now renders a run of nested same-operation compositions as one join, the way `Justification`
has always rendered it as one heading.

```csharp
private IReadOnlyList<BooleanResultBase<TMetadata>> ReasonRun =>
    field ??= RunFlattener.Flatten(_causalResults, RunContinuedBy);

private IEnumerable<BooleanResultBase<TMetadata>>? RunContinuedBy(BooleanResultBase<TMetadata> operand) =>
    operand.Description is BinaryBooleanResultDescription<TMetadata> nested
    && nested.Statement == Statement
    && nested.CausalOperandCount > 1
        ? nested._causalResults
        : null;
```

`ComposeReason` and `Explained` read `ReasonRun` where they read `_causalResults`. Nothing else in the
rendering changed: `ExplainReason`, `Separator` and `IsSameFamily` are untouched, and so is
`CausalOperandCount`, which several tests read directly.

The `code-simplifier` pass extracted the walk. `FlattenRun` as first written was
`BooleanResultExtensions.FlattenCollapsible` — the justification's own collapse — transcribed with a
different predicate, so the two are now one iterative helper, `Motiv.Traversal.RunFlattener`, differing
only in the continuation each supplies. That is the right seam: **the two renderers should share the
walk and not the predicate**, for the reason the next section gives.

## The measurement refutes the ticket's diagnosis

#139 proposed closing itself against #137:

> Likely the same defect as #137: `RuleEvaluationResult` reads `Values`, and #137 records the metadata
> tier as quadratic-plus. If so this is not a separate bug, and this ticket should be closed against
> that one once confirmed.

Timing each of the projection's six reads separately, on a fresh result each time — the tree memoises
as it is read, so a shared result would credit whichever read ran first — and fitting an exponent to
allocated bytes over 25 → 1,600 operands of a left-nested `And` chain:

| Read | Exponent | Bytes at n=1600 | |
|---|---|---|---|
| `Values` | **1.00** | 848 KB | the suspected cause — linear |
| `Justification` | **0.95** | 699 KB | linear |
| `Assertions` | 1.59 | 10.9 MB | |
| `Explanation` (tree walk) | 1.67 | 53.6 MB | |
| `Reason` | **1.88** | **51.8 MB** | the dominant term |
| whole projection | 1.68 | 128 MB | |

`Values` is flat linear. The suspicion was reasonable when filed and is simply out of date: #136 removed
the cyclic source edge, #137 held the cure with six red-proved cases, #195 made the tier's union lazy
and #193 cached the root projection. **A ticket's diagnosis is a hypothesis about code its author did
not re-read** — the third time in this series that measuring one has corrected it, after #192 and #193.

The dominant term is `Reason`, and it is overhead rather than answer: the root's reason over a
1,600-operand chain is a **32 KB** string, built at a cost of **51.8 MB**. Per #195's rule — a square
that is the size of what was asked for is not a defect — this one is not that.

## The mechanism: one class, two renderers, one collapse

Both renderings are produced by `BinaryBooleanResultDescription` through the same `PostOrderFold`. They
differed in the operand list they folded over:

| | operand list | over a left-nested run of n |
|---|---|---|
| `Justification` | `Collapsed` — `_causalResults.FlattenCollapsible(Statement)` | one heading, n children — **Θ(n)** |
| `Reason` (before) | `_causalResults` — the two direct operands | n levels, an O(k) string at level k — **Θ(n²)** |

And the square was **retained**, not merely transient: `FoldedReason` memoises each level in
`_foldedReason`, so a chain of 1,600 kept ~50 MB alive to answer with 32 KB.

Nothing about this was hidden. The collapse was sitting in the same file, eight lines away, doing the
job for the other renderer. It went unnoticed because the two renderers are read for different
purposes and nobody had a reason to compare their costs until the decision log put both on the
evaluation path of every audited rule.

> **A working sibling is the cheapest correctness argument available.** The fix is not a scheme
> invented for `Reason`; it is the scheme `Justification` was already using, applied to its twin. That
> is what made the equivalence provable rather than merely tested.

## Why the two renderers share the walk but not the predicate

The reason renderer's collapse condition is *stricter* than the justification's, in two ways that each
cost a red test to find. Both are the same principle: **collapsing is sound exactly where the operand's
reason was already being reproduced verbatim.**

### Guard 1 — the same statement, not the same family

`ExplainReason` passes a same-*family* operand's reason through unchanged, and `And`'s family admits
`AndAlso`. But `AndAlso` joins with `" && "`:

```
(a == true) && (b == true) & (c == true)
```

Collapsing on family would rewrite that to `& & &`. The run may only be collapsed across one
**operation** — which is exactly the condition `FlattenCollapsible` already applies, since `Statement`
and `Separator` are in bijection across the four subclasses. So the reason renderer inherits the
justification's notion of where a run ends, and the two now agree about it.

This guard was written before the change and **passed even against the naive flatten** — the
`Statement` comparison was in the first version. It is kept because it is the only record of why the
condition is `Statement` rather than `IsSameFamily`, which is the reading the surrounding code invites.

### Guard 2 — more than one causal operand

This one the naive version got wrong. A composition that contributed a *single* cause renders as that
cause's reason verbatim: `ComposeReason`'s `1 => operandReasons[0]` returns it without consulting
`ExplainReason`, so an equality assertion arrives **unparenthesised**. Where `x` is false, `y` true and
`c` false:

```
x == false & (c == false)
```

Collapse that inner `And` and its cause becomes an operand of the outer one, which *does* parenthesise
an equality assertion — `(x == false) & (c == false)`. A silent rewrite of a rendering that adopters
read and that the example suites assert on.

**Both guards were proved to bite before being trusted.** The naive flatten — same `Statement`, no
causal-count condition — was implemented first and run:

- `Should_not_collapse_an_operand_that_contributed_a_single_cause` — **red**
- `DescriptionBaselineTests` — **red**

A gate is only worth what it refuses (4I's lesson, and it keeps generalising). Writing the guard first
and never seeing it fail would have proved nothing about it.

## What the corpus oracle is worth here, and what it is not

`DescriptionBaselineTests` hashes the rendering of every node of a seeded generated corpus — roughly
600 KB of `Reason` and `Justification` text — against a baseline captured from the recursive formatters
before Spec 3A replaced them. It is the instrument that establishes this change as byte-identical, and
it caught the naive version immediately.

It is worth restating the limit the series already recorded, because it cuts the other way here.
**A differential oracle transcribed from shipped code asserts that behaviour never changes, not that it
is correct** — `StackSafeTraversalOracleTests` was green throughout Spec 3A while `RootAssertions` was
wrong. That property is a liability when the change is a fix and an asset when the change must be
invisible. This change must be invisible, so the oracle is doing exactly the job it is good at: it
cannot tell us the rendering is *right*, only that it is the same rendering, which is the whole claim.

Note in passing that `x == false & (c == false)` — guard 2's case — is arguably inconsistent
parenthesisation. It is not this slice's to change. Preserving it is the point.

## Result

| | before | after |
|---|---|---|
| `Reason` exponent | 1.88 | **0.98** |
| `Reason` bytes at n=1600 | 51,831,640 | **547,528** |
| whole projection exponent | 1.68 | 1.58 |
| whole projection at n=1600 | 128 MB | 76.9 MB |

Rendering unchanged, by the corpus hash. 5,934 tests in `Motiv.Tests` on net8/9/10, plus
`Motiv.Serialization` (980 × 3), `Motiv.Serialization.AspNetCore` (162), `Motiv.Studio` (109),
`Motiv.CodeFix` (94), `Motiv.Analyzer` (20) and the five example suites — all green. The solution
builds for `net472` and `netstandard2.0` with no warnings.

## What this leaves open, and why the ticket is not closed

Two residues, both measured, neither fixed here.

**1. The collapse only reaches a homogeneous run.** An *alternating* `&`/`|` chain has no run to
collapse, so `Reason` stays at exponent **1.93** there (139 MB at n=1,600). This is a limit of the
approach, not a bug in it: the collapse removes a level whose text was already being reproduced
verbatim, and an alternating chain has no such level. Removing this residue means rendering into a
single buffer and folding offsets rather than strings — a much larger change to twelve formatters,
which is a slice of its own and not this one.

**2. The projection is still superlinear — exponent 1.58 — now dominated by the explanation tree**
(1.67) and `Assertions` (1.59). Both come from the same shape in different machinery:
`BooleanResultBase.SourcesOf` materialises and memoises *every* node's full source list, so a chain of
n retains Σk arrays, and `Explanation.Resolve` then reads one per level. **This is the metadata twin's
defect, one property over.** #195 made the metadata tier's union lazy for exactly this reason and the
assertion side never got it — the third twin asymmetry the series has turned up, after #189's and
#192's.

So `ToEvaluationResult` is faster and no longer quadratic in its worst term, but #139's own title —
"materialising a result projection is superlinear" — is still true. **Closing it would be a claim the
measurement does not support**, which is the failure mode #137 was filed against in the first place. It
stays open, re-scoped by comment to the residue above.

**One cost that is not a defect and gets no follow-up.** On an alternating chain `Justification`
measures exponent 2.77 — but its *output* at n=1,600 is 10,273,285 characters, because nested
alternating operators indent everything beneath them and the text genuinely grows with the square. The
square is in the answer, not in the walk. Same conclusion #193 reached about reading `RootValues` at
every level, and worth recording so that a later reading of this table does not file it.
