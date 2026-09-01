# Spec 3A follow-up — a leaf is not its own source — Design

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1. Ticket [#188](https://github.com/karlssberg/Motiv/issues/188); the plan is
[here](../plans/2026-09-01-spec-3a-followup-source-fallback.md).

Spec 3's first invariant — *"every public result-tree property behaves identically at every depth"* —
is about stack-safety, and 3A satisfied it. This slice is about a different kind of uniformity the
same audit exposed: the three source walks are one algorithm, so they must give one answer, including
at the bottom.

## What changed

One line, in one place:

```csharp
-        return sources.Count == 0
-            ? [result]
-            : sources.ToArray();
+        return sources.ToArray();
```

`UnderlyingAssertionSources`, `UnderlyingAllAssertionSources` and `UnderlyingMetadataSources` now
return empty for a result with no causes, where they previously returned a single-element sequence
containing the result itself. Deleting the fallback leaves `SourcesOf`'s `result` parameter unused, so
it goes too — the helper now takes only the children and the folded operations.

`RecursiveTraversalOracle` loses the same fallback from the same three walks.

## The principle: navigation is not projection

The ticket presents a genuine two-sided argument and asks the fourth question that decides it — does
`RootValues`' `ElseIfEmpty(Values)` belong in the same sweep?

It does not, and the reason is the reason the fallback was wrong in the first place. Motiv has two
different families here, and they were being read as one:

| | `Underlying*Sources` | `Root*` (`RootValues`, `RootAssertions`, `AllRootAssertions`) |
|---|---|---|
| Answers | *which nodes* are beneath me | *which values* are at the leaves |
| Result type | `BooleanResultBase` | `string` / `TMetadata` |
| Self-answer at a leaf | a category error against "underlying" | correct — the values at a leaf **are** its own |
| Can it hang a descent? | yes: `[this]` is a fixpoint | no: there is nothing to descend |

The case for the fallback — "`Values` is non-empty on a leaf, so empty asserts *nothing produced these
values*, which is false" — is a true statement about the `Root*` family being smuggled into the
`Underlying*` one. `UnderlyingMetadataSources` was never asked *which values*; it was asked *which
nodes*, and the honest answer at a leaf is none. A consumer that wants the leaf's values reaches them
through `Values`, which is what it is for.

That distinction is now pinned by a test
(`Should_leave_the_root_value_projections_falling_back_to_their_own_values`) rather than left as
prose, because the two idioms sit a few properties apart in the same file and read as an
inconsistency without the reason written down.

## Why this is invisible to the library, and why that is a measured claim

The ticket calls this "a genuine breaking change to two properties with real callers". That is true of
the *published contract* and false of the *library's own behaviour*, and the gap matters enough to
state precisely.

Both in-library consumers reach a source walk only behind a guard:

```csharp
// Explanation.Resolve                     // MetadataNode.Resolve
result switch                              cause switch
{                                          {
    IBooleanOperationResult => sourcesOf(result),   IBooleanOperationResult<TMetadata> => cause.UnderlyingMetadataSources,
    _ => result.ToEnumerable()                      _ => cause.ToEnumerable()
}                                          }
```

So the fallback was reachable from inside the library only if some operation node had an empty causal
set. None does: `GetCausalResults` is total for every operator — satisfied implies some operand
matched, unsatisfied implies some operand did not — so an operation always has a causal child.

That premise is the whole safety case, so it is now a test rather than an argument
(`Should_never_present_an_operation_node_with_an_empty_causal_set`, over all 150 seeds). If it ever
stops holding, `Explanation.Underlying` and the metadata tier tree start changing shape, and a test
says so instead of a user noticing.

**The corroborating evidence:** the whole solution — thirteen test projects, 7,217 tests, including
`StackSafeTraversalOracleTests` comparing every walk at every node of every generated tree — passed
with no change beyond the three source-walk suites themselves. `Reason`, `Justification`,
`Assertions`, `Values`, `RootValues`, `RootAssertions` and `Explanation.Underlying` are all untouched.

## Measurement

Over the Spec 3A corpus — 13,680 nodes across 150 seeds:

| | Count |
|---|---|
| Nodes whose answer changes (no causes) | **6,109** |
| Operation nodes | 5,895 |
| Non-operation nodes that do have causes (all higher-order) | 1,676 |

Two facts in that table are worth more than the headline number.

**The three walks go empty on exactly the same 6,109 nodes.** Not the same count — the same nodes,
with no node at which one walk falls silent and another does not. "Wholesale" was #136's stated design
constraint, expressed in prose as *"two of three walks falling back, one not, with no principle
separating them"*. It is now
`Should_report_no_sources_at_the_same_nodes_for_all_three_walks`, so a future change that re-splits
the family fails a test instead of passing review.

**5,895 + 6,109 + 1,676 = 13,680 exactly**, which says every non-operation node with causes in the
vocabulary is a higher-order result. That is why the higher-order case gets its own test: it reaches
the empty branch by a different route than an atomic proposition — a `HigherOrder…FromBooleanPredicate`
result exposes no `Causes`, `Underlying` *or* `CausesWithValues`, and is not an operation result, so
nothing about the atomic case implies it.

## The cost, stated plainly

`RecursiveTraversalOracle` exists to freeze pre-3A semantics: *"a later change that quietly alters
traversal semantics fails a test rather than passing review."* Three of its members no longer do that.
Since #136 one member was already not verbatim; now all three source walks are not.

This is a real reduction in what the oracle guarantees, not a bookkeeping detail, and the class remark
now says so: for those three the question is only *"does the fold match an independent recursive
formulation?"*, not *"does the fold match what shipped?"*. The behavioural claim the oracle can no
longer make is carried instead by this slice's own tests, which is the only place it can live once the
change is deliberate.

The alternative — keeping the oracle at the old semantics and letting the differential test fail — is
not an alternative. A gate that is expected to be red is a gate nobody reads.

## A second-order effect worth recording

#136's `Should_never_report_an_operation_result_as_a_source` had to exclude nodes with no causal
values, with a comment naming this ticket as the reason:

> Nodes with no causal values are excluded because the `ElseIfEmpty` fallback makes such a node its own
> source — which would be an operation result if one ever had an empty causal set.

The exclusion existed only to insure against the fallback. With the fallback gone the claim widens to
every node of the corpus for free. A defect does not only produce wrong answers; it narrows what the
tests around it are allowed to say, and the narrowing outlives the defect unless somebody goes back
for it.

## What the review pass found

The `code-simplifier` round produced four applied edits, all clarity, no behaviour: a class remark
that said "four members" while naming three; a doubled `<see cref="Values"/>` in one sentence; a
`Sum(…).ShouldBeGreaterThan(0)` that walked all 150 corpora to compute a total it never used, replaced
by a short-circuiting `SelectMany(…).ShouldNotBeEmpty()`; and asymmetric type arguments on three
sibling calls.

It also found a real gap. `Should_report_no_sources_for_a_higher_order_result_that_exposes_no_causes`
asserted `Causes.ShouldBeEmpty()` as its premise and then made three claims — but the other two walks
descend `Underlying` and `CausesWithValues`, so two thirds of the premise rested on the test passing
rather than being stated. All three are now asserted. This is the same shape as 4H's lesson: *a claim
about a check is not checked by that check passing.*

Of its two larger suggestions, one was declined and is recorded in the plan's out-of-scope section.
The other — nine repetitions of the same corpus walk across four files in `src/Motiv.Tests/Traversal/` —
was declined for scope and then taken anyway, as a separate commit, once it proved to be nine
mechanical call sites and no assertion changes. The plan says why it could not simply wait for a
standalone branch.

## Release note

The three `Underlying*Sources` properties — `UnderlyingAssertionSources`,
`UnderlyingAllAssertionSources` and `UnderlyingMetadataSources` — return an **empty** sequence for a
result with no causes, where they previously returned a single-element sequence containing the result
itself. Composite results are unaffected.

This is source-compatible and behaviour-breaking, so it belongs to a major release. A consumer that
relied on the old shape was relying on a leaf naming itself as its own underlying source; the two
migrations are:

- `result.UnderlyingAssertionSources` used as "the assertions beneath me, or my own" → use
  `RootAssertions`, which keeps its fall-back-to-self and is the property for that question.
- `result.UnderlyingMetadataSources.SelectMany(s => s.Values)` used the same way → use `RootValues`,
  likewise unchanged.

Nothing else moves: `Reason`, `Justification`, `Assertions`, `Values`, `RootValues`, `RootAssertions`,
`AllRootAssertions`, `Explanation.Underlying` and `MetadataTier` are all identical at every node of the
corpus.
