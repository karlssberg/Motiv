# Spec 3A follow-up — `UnderlyingMetadataSources` yields the parent — Design

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the question [Spec 3A](2026-08-24-spec-3a-stack-safe-traversal-design.md) deferred.
Ticket [#136](https://github.com/karlssberg/Motiv/issues/136); follow-up
[#188](https://github.com/karlssberg/Motiv/issues/188).

## Summary

`BooleanResultBase<TMetadata>.UnderlyingMetadataSources` is a public property that structurally
**cannot return a source**. On a bare atomic result it returns nothing; on any composition it returns
ancestor nodes. Spec 3A found the divergence, preserved it verbatim rather than fixing it inside a
stack-safety rewrite, and opened #136 to settle whether it was design or accident.

It was accident. This slice corrects it, and unifies the three source walks onto one implementation so
the same drift cannot recur.

## What the property actually returned

Probed against `Motiv` as shipped, with three atomic explanation propositions:

| Expression | `UnderlyingMetadataSources` | `UnderlyingAssertionSources` |
|---|---|---|
| `a` (atomic, true) | *(empty)* | `a-true` |
| `a & b & c` (`c` false) | `AndBooleanResult:c-false` | `c-false` |
| `a \| b \| c` (`a`,`b` true) | `OrBooleanResult:a-true \| b-true` ×2 | `a-true`, `b-true` |

The middle row is the whole defect in one line: asked for the sources of its values, the result named
*itself*. The bottom row shows the multiplicity — the inner `Or` node returned twice, once per
non-operation child it had.

## Why nobody noticed

Three things had to line up, and did.

**`Values` never reads it.** `MetadataTier`'s metadata comes from `CausalResults.GetValues()`, not
from this walk. So `Values`, `Reason`, `Justification` and `Assertions` are all correct on a tree
whose `UnderlyingMetadataSources` is nonsense. The property is public, but the library's own hot path
routes around it.

**Its one internal consumer compensates.** `MetadataNode.Resolve` calls it — and only ever on
operation results:

```csharp
cause switch
{
    IBooleanOperationResult<TMetadata> => cause.UnderlyingMetadataSources,
    _ => cause.ToEnumerable()          // the correct step, hand-written
}
```

`Resolve` writes the correct outer step itself and delegates only the recursion. That both hid the
defect *and* is the strongest single piece of evidence about intent: the shape the walk was supposed
to have is written out longhand, three lines from the call.

**The two halves live in disjoint parts of the tree.** "Yields the parent" bites only at operation
nodes; the missing fallback bites only at leaves. No single test shape exercises both, so no test ever
had cause to look.

## The evidence the decision rested on

#136 asked for a decision rather than a patch, because this changes what a published property returns.
Three measurements answered it.

**1. Nothing asserted the shipped behaviour.** Applied experimentally and run across all eight test
projects: 151 failures, of which 150 were seeds of `StackSafeTraversalOracleTests` and one was the
probe itself. Zero behavioural tests — none in `Motiv.Tests`, the three example suites, Studio, the
serialization stack or Blazor — depended on the shipped behaviour. The only thing holding the old
shape in place was the gate written to hold it in place.

An empty test diff is weak evidence, though, and it was nearly taken for a strong one: see
*The repair is wider than the ticket* below, where measuring rather than inferring found a documented
public property that changes.

**2. The difference has the shape of a slip, not a design.** `this` where a sibling has
`booleanResult`, inside a `SelectMany` lambda, plus an absent `.ElseIfEmpty`. That is what a
copy-paste which missed two renames looks like. A deliberate divergence would have left a note; 3A's
XML remark was the *first* note, added eight years and two majors later by someone reading it as
suspicious.

**3. Nothing anywhere describes it as intended.** No doc, no test name, no commit message.

## The design: unify rather than patch

The one-line fix was available — `sources.Add(result)` → `sources.Add(child)`, plus the fallback. It
was rejected in favour of removing the duplication that produced the drift.

`AssertionSourcesOf` became generic:

```csharp
private protected static TResult[] SourcesOf<TResult>(
    TResult result,
    IEnumerable<TResult> children,
    IReadOnlyList<TResult[]> foldedOperations)
    where TResult : BooleanResultBase
```

and all three combiners collapsed to one line each:

```csharp
CombineCausalAssertionSources = (result, folded) => SourcesOf(result, result.Causes,           folded);
CombineAllAssertionSources    = (result, folded) => SourcesOf(result, result.Underlying,       folded);
CombineMetadataSources        = (result, folded) => SourcesOf(result, result.CausesWithValues, folded);
```

> **Since superseded.** The two blocks above are what *this* slice shipped and are left as written,
> because a design doc that describes code its slice did not write is worse than one that is dated.
> [#188](https://github.com/karlssberg/Motiv/issues/188) then removed the fallback-to-self, which left
> the `result` parameter unused, so it went too — `SourcesOf` now takes only `children` and
> `foldedOperations`, and the three combiners read `SourcesOf(result.Causes, folded)` and so on. Read
> the current signature from the source, not from here.

The three walks are no longer three copies that happen to agree. They are one implementation handed
three different child-sets, which is what ticket 19's audit said they were all along. The pattern was
already in the file — `Operations<TResult>`, two methods above, uses the same
`where TResult : BooleanResultBase` trick to serve both the untyped and `TMetadata`-typed walks. The
metadata combiner simply had not been brought into it.

This is not a violation of `CLAUDE.md`'s "avoid over-DRYing". That rule protects *deliberate*
duplication between proposition families with nuanced differences. Here the duplication is not
deliberate and there is no difference — the audit called them the same algorithm, and the divergence
is the bug being fixed.

## The repair is wider than the ticket

#136 describes a property with no consumers. That framing survived the first pass of this slice and
was wrong, and the mandatory `code-simplifier` review is what caught it: *"no test noticed" and
"nothing changed" aren't the same claim.*

So the claim was measured instead of inferred. Every node of the 150-seed corpus was characterised
before and after the fix, across four surfaces:

| Surface | Differing nodes (of 13,680) |
|---|---|
| `Values` | **0** |
| `MetadataTier.Metadata` | **0** |
| `MetadataTier.Underlying` | 1,000 |
| **`RootValues`** | **566** |

`RootValues` is public and documented — *"the metadata yielded by all results that evaluated"*. In all
566 differing nodes the post-fix value is a **strict superset** of the pre-fix one; not once is it a
subset, and not once are values merely exchanged. The old walk was dropping metadata.

A representative node, from seed 20:

```
((not 4 == true) ^ (not 1 == true)) & (under 3 == false)

before:  RootValues == [n != k]
after:   RootValues == [n != k, not less than 3]
```

`under 3 == false` is a causal operand of that `And`. It evaluated, it contributed, and its metadata
was absent. The chain is direct: `MetadataNode.Resolve` builds the tier tree from
`UnderlyingMetadataSources`; that walk returned ancestors; so the tier tree was built over composite
tiers instead of operand tiers, and `RootValuesOf` descended a tree that no longer reached the right
leaves.

This inverts the ticket's premise in the way that matters. `UnderlyingMetadataSources` looked inert
because the library routes around it — but its one consumer feeds a property that consumers *do* read,
so the defect was live all along. It also removes the last of the doubt about intent: no reading of
"deliberate" survives a walk that silently discards contributing operands' metadata.

`Values` being untouched is the reason it stayed invisible. The surface most people read was never
wrong.

### The residue: higher-order subtrees

The fix does not repair `RootValues` completely. Compared against an independent descent of
`CausesWithValues` to the causal leaves, post-fix `RootValues` still disagrees at 2 corpus nodes — and
the boundary is exact:

| Nodes | Mismatches after the fix |
|---|---|
| Subtree contains a higher-order result | 2 |
| Subtree contains no higher-order result | **0** |

The suspect is `MetadataNode.Resolve`'s collapse rule rather than the source walks: a higher-order
result expands one cause into many tiers, and plain sibling operands appear to be collapsed through
and lost. That is [#189](https://github.com/karlssberg/Motiv/issues/189), out of scope here.
`Should_reach_every_causal_leaf_from_RootValues` asserts the invariant with exactly that exclusion,
names the ticket, and fails on 33 of 150 seeds without this slice's fix — so it is a real gate on the
repair, not a restatement of current behaviour.

## What the oracle had to become

`RecursiveTraversalOracle` is documented as "a verbatim copy of the recursive walks as they stood
before Spec 3A". `UnderlyingMetadataSources` is now the single member that is not, and it carries a
remark saying so.

That is deliberate. A differential oracle exists to catch *unintended* semantic change. Leaving it
verbatim after #136 would have pinned the defect in place permanently — the gate would fail the fix
and pass the bug, which is the opposite of what it is for. The remark records the exception so the
next reader does not "restore" it.

## What was deliberately left undone

The fallback added here answers "what are my sources?" with **myself** when nothing contributed. That
is the siblings' behaviour, and matching it is the point. Whether it is *right* is a separate
question, raised as [#188](https://github.com/karlssberg/Motiv/issues/188) and since settled the other
way — all three walks now return empty; see
[that slice's design](2026-09-01-spec-3a-followup-source-fallback-design.md).

Measured over the oracle corpus — 13,680 nodes across 150 seeds — the fallback fires **6,109 times,
every one on a node with no causes, and never on an operation node**. That is not an artefact of the
corpus: for every binary operator `GetCausalResults` is total (satisfied ⇒ some operand matched;
unsatisfied ⇒ some operand did not), so an operation node always has a causal child. The open question
is therefore exactly one thing: *what should an atomic result report as the source of its own values?*

There is a real argument for empty. The name says `Underlying`, and a leaf has nothing underlying it;
worse, `[this]` makes the family non-terminating under the obvious fixpoint descent, because a leaf
names itself as its own underlying source. There is a real argument for self: `Values` is non-empty on
a leaf, so empty asserts "nothing produced these values", which is false — and the same
`ElseIfEmpty`-to-self idiom appears in `RootValues` a few properties away.

It could not ride along here for a reason that is structural rather than about caution.
`UnderlyingAssertionSources` and `UnderlyingAllAssertionSources` **have consumers**;
`UnderlyingMetadataSources` had none, which is how its defect survived. Changing the fallback for the
metadata walk alone would re-create precisely the inconsistency #136 exists to remove — two of three
walks falling back, one not, with no principle separating them. The change is wholesale or not at all,
and wholesale is a breaking change to two properties with real callers, wanting a major rather than
the patch this could take.

The unification makes that follow-up cheap: the fallback now lives in one place instead of three.

## Verification

| Obligation | How |
|---|---|
| Corrected walk yields children, not ancestors | `Should_yield_the_child_the_walk_stopped_at_rather_than_the_result_itself` |
| No operation node is ever reported as a source | `Should_never_report_an_operation_result_as_a_source`, over the 150-seed corpus |
| Fallback restored | `Should_yield_itself_when_nothing_contributed` |
| The three walks really are one algorithm | `Should_agree_with_its_assertion_source_sibling_wherever_the_causal_sets_agree` — asserts equality with `UnderlyingAssertionSources` at every corpus node whose two causal sets coincide |
| **`RootValues` reaches every causal leaf** | `Should_reach_every_causal_leaf_from_RootValues` — fails on 33 of 150 seeds without the fix |
| No unintended traversal change | `StackSafeTraversalOracleTests`, 150 seeds, every member at every node |
| No behavioural regression | All eight test projects on net10.0 |
| CI's frameworks build | `dotnet build Motiv.slnx`, net472 included |

## Consequence for consumers

Two public properties change relative to v9.1.0.

**`UnderlyingMetadataSources`** returns different objects. Since the old return value was
ancestors-or-nothing, any caller relying on it was relying on something that could not have been what
they wanted; there is no plausible migration burden.

**`RootValues`** returns *more* metadata for compositions — strictly more, never less, on 566 of the
13,680 corpus nodes. A consumer that renders `RootValues` will see operands appear that were
previously missing. That is the point of the fix, but it is the change a reader of the release notes
actually needs, because it is the one visible in output. `Values`, `Assertions`, `Reason` and
`Justification` are unaffected.

`MetadataTier.Underlying` also changes shape (1,000 nodes) — it now exposes operand tiers where it
previously exposed composite ones.
