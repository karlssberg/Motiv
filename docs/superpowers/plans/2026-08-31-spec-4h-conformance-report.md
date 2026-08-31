# Spec 4H — The Conformance Report — Implementation Plan

**Design:** [2026-08-31-spec-4h-conformance-report-design.md](../specs/2026-08-31-spec-4h-conformance-report-design.md)
**Ticket:** [#163](https://github.com/karlssberg/Motiv/issues/163)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§4 — the **VPAT / Accessibility Conformance Report** ticket
[18](https://github.com/karlssberg/Motiv/issues/118) names as an explicit output, *"procurement asks
for the document"*

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). Ticket #163 states both wrong claims with
> their reproductions, prescribes the record-and-gate shape, and even predicts the fallout; it is not
> repeated here.

## Global constraints

- **The bug is in a *claim about* a suite, not in the suite.** Nothing that runs could have caught
  either defect, because the axe sweep passing says nothing about whether a sentence describing axe
  is true. That shapes everything: the fix is a join between the record and axe's own tags, checked.
- **TDD, and here it is literal.** The gate is a `vitest` suite, so each of its checks was written
  first and shown to reject the record that shipped before this slice — starting with the two rows
  #163 names.
- **A check that cannot fail is not a check**, and one of them nearly wasn't: the keyboard-title
  check is vacuous if its source regex matches nothing, so it carries a guard on itself.
- **Answer for all fifty, or the omissions are the claim.** Partial enumeration is what produced both
  defects; the record is not allowed to be partial.
- **`axe` stays green throughout.** As in 4D and 4F, nothing here is a violation a scan can see —
  including the one real product defect the slice found.
- **The manual pass stays outstanding**, and every document must keep saying so rather than rounding
  it up.

## File structure

```
ui/apps/studio/a11y/criteria.ts               (new — the catalogue: 50 criteria, WCAG_AA, axeTagFor)
ui/apps/studio/a11y/conformance.ts            (new — 557 lines: the record, one row per criterion)
ui/apps/studio/a11y/report.ts                 (new — the renderer, escapeCell, enabledAxeRules)
ui/apps/studio/test/a11y/conformance.test.ts  (new — the gate: 7 refusals + the escaping tests)
docs/accessibility/vpat.md                    (new — generated; do-not-edit banner)
docs/accessibility/toc.yml ; docs/accessibility/index.md ; docs/Overview.md ; CLAUDE.md
ui/apps/studio/e2e-a11y/axe.spec.ts           (WCAG_AA imported, no longer redeclared)
ui/apps/studio/src/routing/useDocumentTitle.ts (new — the 2.4.2 defect the enumeration found)
ui/apps/studio/test/routing/useDocumentTitle.test.ts ; src/App.tsx
ui/apps/studio/package.json                   (`a11y:report`)
ui/apps/studio/tsconfig.json                  (`a11y` added to `include`)
```

Fifteen files, +1,425/−40.

## Sequence

1. **Check the fourteen published rows against the suite they cite**, before writing anything. Two
   are wrong, in opposite directions. Confirm 1.4.11 against axe directly —
   `axe.getRules().filter(r => r.tags.includes('wcag1411'))` returns `[]` — and 1.4.4 the same way,
   where `meta-viewport` is tagged `wcag144` and has been running all along.
2. **Write the catalogue**: all fifty Level A and AA criteria in WCAG order, the `WCAG_AA` tag set,
   and `axeTagFor` — the join between a criterion number and axe's tag vocabulary.
3. **Share the tag set with the sweep.** `axe.spec.ts` redeclared it; it now imports the one
   definition, so "enforced by axe" is true of the suite that runs rather than one someone remembers.
4. **Write the gate first**, against the *old* record, so each check is seen rejecting a real defect.
5. **Write the record**: fifty rows, each with a verdict, evidence and a remark. 1.4.11 becomes
   `Not Evaluated`; 1.4.4 gains its `axe` claim.
6. **Write the renderer**, resolving `axe` claims to rule ids at render time and publishing the
   structural arguments rather than leaving them in source.
7. **Wire regeneration to the gate itself** — `a11y:report` is the drift test in snapshot-update
   mode, so generator and gate cannot diverge.
8. **Take the fallout the ticket predicted.** Enumerating 2.4.2 Page Titled surfaces a real defect:
   three hash routes sharing one static title. `useDocumentTitle` writes it from the route.
9. **Break every check** to confirm each still fails.
10. **Rewrite the prose page** around the generated report, and point the auditor at the record.

## The follow-up commits

Two, both from review, both real:

- **CodeQL** on the table-cell escaping. Escaping `|` without first escaping `\` is incomplete, and
  the failure mode is a shifted table in a document that exists to be read as a table.
- **Copilot** on the audit instructions, which told an auditor to record findings in the generated
  report — a file with a do-not-edit banner and a drift gate that would reject exactly that edit.

## Not run

No C# is touched, so no .NET suite applies. The a11y sweep and the keyboard suite need a browser and
a built Studio; both ran in CI on the tagged head.
