# Spec 3A follow-up — the same defect in the assertion tree — Plan

**Date:** 2026-09-03
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the twin
[#189](https://github.com/karlssberg/Motiv/issues/189) measured and declined (PR
[#194](https://github.com/karlssberg/Motiv/pull/194), plan
[here](2026-09-01-spec-3a-followup-root-values-branches.md)). Tracked as
[#192](https://github.com/karlssberg/Motiv/issues/192).

Not a build-map slice: #192 is a bug ticket spawned by 3A, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no row in that map's slice table —
the same call #136, #188, #189, #137 and #195 made. It is recorded on #169 under the follow-ups the
shipped slices spawned.

## The debt being paid

`RootAssertions` is documented as *"the assertions from the root causes of a boolean result, instead
of causes from possible intermediate propositions"*. For a composition with a higher-order result
anywhere in its subtree, it dropped the assertions of operands that plainly evaluated and plainly
contributed. On corpus seed 7, node `(all neighbours == true) && !(divisible by 2 == false)`:

```
node.RootAssertions        == ["under 2 == false"]
causal leaves of that node == ["under 2 == false", "divisible by 2 == false"]
```

The boundary is exactly #189's, which is what one expects of the same bug in the twin tree:

| Nodes | Disagreements with an independent causal-leaf descent |
|---|---|
| Subtree contains a higher-order result (2,063) | **84** |
| Subtree contains none (11,617) | **0** |

## The decision

The diagnosis is #189's and is not re-derived — its design doc carries it in full, including the
148-test refutation of the obvious fix. In short: `Explanation.Underlying` is lossy twice by design
(it drops a level that merely restates its children, and it flattens away branch identity), "the
deepest explanation" is a property of a **branch**, and a higher-order result is the only node type
that makes one branch deeper than its siblings. The collapse is correct; the walk over it is not.

So the fix mirrors the metadata one exactly: a new internal `Explanation.Branches` — the un-collapsed
`CausalResolution.Children` — and `GetRootAssertions` folds from the result's own explanation
descending it, with the per-branch fallback that `CombineRootAssertions` already had.
`Explanation.Underlying`, `SubAssertions` and the collapse rule are untouched.

## What the ticket predicted that the measurement refuted

#192 says *"`AllRootAssertions` needs the same treatment against `Underlying`"* and proposes a
`Branches` over `AllResolution.Children` for it. Measured against an independent descent, it disagrees
on **0 of 13,680 nodes** — it never had the defect, because `GetAllRootAssertions` does not walk the
`Explanation` tree at all. Its invariant is added anyway, as the guard that keeps that true.

## Explicitly out of scope

**`RootValues`' missing cache** ([#193](https://github.com/karlssberg/Motiv/issues/193)), still open
and untouched by this change.

## Steps

1. **Failing tests first.** A corpus-wide invariant asserting `RootAssertions` equals an independent
   descent of `Causes` to the causal leaves, with no higher-order exclusion — the acceptance test #192
   names — plus its `AllRootAssertions` sibling against `Underlying`.
2. **Watch them fail for the right reason** — 14 of 150 seeds, each dropping exactly one causal
   operand's assertion, and `AllRootAssertions` green throughout.
3. **Measure the boundary before touching anything**, to confirm the ticket's 84/2,063 and 0/11,617
   rather than take them on trust, and to establish that the walk only ever under-reports.
4. **Implement**: `Explanation.Branches`, and a `GetRootAssertions` that folds from
   `result.Explanation` over it.
5. **Amend the Spec 3A differential oracle in step.** `RecursiveTraversalOracle.GetRootAssertions` is
   a verbatim transcription of the pre-3A recursion, so it encodes the defect; it joins the three
   source walks that #136 and #188 already amended, with its claim weakened the same way and the
   behavioural claim moved to the new tests.
6. **Keep the corpus's premise honest** — assert separately that the corpus still reaches higher-order
   results, or a generator change would leave the invariant green while covering nothing.
7. **Full solution suite**, plus a timing comparison against the pre-change walk, since `Branches`
   visits more nodes than `Underlying` did.
8. **`code-simplifier` pass**, per `CLAUDE.md` — four clarity edits, one decline, and a mutation
   check it started that had to be re-run safely.

## Verification

- All thirteen test projects green on net10.0; `Motiv.Tests` also green on net8.0 and net9.0 —
  5,923 tests each.
- `StackSafeTraversalOracleTests` green after the oracle is amended, and red before it — which is the
  evidence that the oracle was pinning the defect rather than checking for it.
- `DeepCompositionTests` green, including the small-stack `RootAssertions` ceiling and the
  `SubAssertions`/`Explanation.Underlying`-are-empty contracts.
- Both hand-written tests go red against the pre-change production files, re-checked after the fact on
  committed work rather than on unstaged edits.
- Over the corpus, exactly **84 of 13,680 nodes** change, every one in a higher-order subtree, every
  one gaining assertions and none losing any.
- **No existing test's assertions changed**, despite the 46 lines the ticket flagged across the test
  suites and example projects. The only edits to existing test files are the oracle's amended
  `GetRootAssertions`, and the review pass lifting three shared fixtures out of
  `UnderlyingMetadataSourcesTests`.
- net472 is built but not run: no `mono` host on this machine, a standing local limitation rather than
  anything this change introduces. CI runs it.
