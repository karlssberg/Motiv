# Spec 4J — The Failure Channel — Implementation Plan

**Design:** [2026-08-31-spec-4j-failure-channel-design.md](../specs/2026-08-31-spec-4j-failure-channel-design.md)
**Ticket:** [#170](https://github.com/karlssberg/Motiv/issues/170), closing
[#150](https://github.com/karlssberg/Motiv/issues/150)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§2 and §4

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). The sequence below is what the build actually
> did, including the round the review added.

## Global constraints

- **TDD throughout.** Failing test → confirm it fails for the right reason → minimum code → green.
- **The sibling is the reference, not the template.** `PropositionWorkflowController` sets the error
  *contract*; where the rule loop diverges (a failed load keeping `loaded`), the divergence is argued in
  a comment at the divergence, not smoothed away for symmetry's sake.
- **`rules-core` stays framework-free.** The controller is the whole behaviour; React sees only
  `useRuleWorkflow`'s existing pass-through. §7's obligation — `rules-core` builds and tests with no
  React present — is a standing gate, not a step here.
- **The banner is a11y surface.** Anything rendering it must stay reachable by the axe sweep, which means
  the sweep needs a way to *reach* it — a view that only a broken server produces.
- **UI-only slice.** No C# is touched, so the .NET suites are out of scope rather than skipped.

## File structure

```
ui/packages/rules-core/src/workflow/ruleWorkflow.ts   (failure channel + #clearFailure)
ui/packages/rules-core/test/workflow-rule.test.ts     (10 new cases)
ui/apps/studio/src/shell/ReportBanner.tsx             (new — the one banner)
ui/apps/studio/test/shell/ReportBanner.test.tsx       (new)
ui/apps/studio/src/panes/RuleHeader.tsx               (renders failure; adopts ReportBanner)
ui/apps/studio/src/panes/PropositionsPage.tsx         (adopts ReportBanner)
ui/apps/studio/test/panes/RuleHeader.test.tsx         (2 new cases)
ui/apps/studio/src/styles/app.css                     (.conflict-banner → .report-banner)
ui/apps/studio/src/styles/tokens.css                  (--danger #d1435b → #a8293f, ratios restated)
ui/apps/studio/e2e-a11y/axe.spec.ts                   (the rules page, API broken)
docs/accessibility/index.md                           (the new view, and why it needed breaking)
```

## Sequence

1. **The failing test.** A `listRules` that rejects: assert the rejection reaches `state.failure`
   rather than the caller. Confirm it fails as an unhandled rejection — the exact defect #150 names —
   and not merely as an absent field.
2. **The channel.** `failure: string | null` on `RuleWorkflowState`, the private field, the snapshot,
   and `refresh`'s `try`/`catch` through `describeUnexpectedFailure`.
3. **`load` and `save`**, one at a time, each with its own failing case first. `load`'s carries the
   decision that `loaded` survives; `save`'s asserts a version conflict is *not* written into `failure`.
4. **Supersession.** Three cases — a superseded refresh, load and save — each asserting the stale failure
   never lands. These reuse the counters already there; the point is that the failure path is gated
   the same way the success path is.
5. **`ReportBanner`.** Extracted from the markup `RuleHeader` and `PropositionsPage` had each copied,
   with the three component tests: it is an `alert`, it offers no button without `onReload`, and it runs
   the one it is given. Both pages switch to it; `.conflict-banner` becomes `.report-banner`.
6. **`RuleHeader` renders the failure**, above the conflict, with the loaded rule as the way back.
   Two page-level tests: a failed listing with no reload offer, and a thrown save whose reload clears
   the banner.
7. **The axe view.** Route the listing to a 503, visit `/#/rules`, assert the alert, scan. Run it.
8. **The contrast fix the scan found.** `--danger` → `#a8293f`, re-measured against the tint it is
   actually drawn on; the token comment restated to name the tightest ground for all four moved colours.
   `docs/accessibility/index.md` gains the view and the reason it exists.
9. **The review round.** Copilot found `refresh` never clearing a standing failure, and `failure`
   documented as "against the loaded rule" while `refresh` reports with nothing loaded. Fixed as one
   rule in one place: `#clearFailure`, called on the way out of all three operations; `save` stops
   clearing on its typed outcome. Two more tests, both watched failing first — a successful refresh
   clears what the previous one raised, and a retried save clears before the PUT lands. The test that
   claimed a typed outcome did the clearing was passing vacuously and now exercises `save`.
10. **The simplification pass**, then the UI suites and the axe sweep green.

## Not run

The .NET suite and `pnpm e2e` (which drives the .NET host) — no .NET SDK in the session
([#173](https://github.com/karlssberg/Motiv/issues/173)). No C# is touched, so this is a disclosure
rather than a gap.
