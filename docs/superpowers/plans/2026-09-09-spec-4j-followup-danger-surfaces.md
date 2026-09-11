# Spec 4J follow-up — The other `--danger` surfaces — Implementation Plan

**Design:** [`2026-09-09-spec-4j-followup-danger-surfaces-design.md`](../specs/2026-09-09-spec-4j-followup-danger-surfaces-design.md)
**Ticket:** [#168](https://github.com/karlssberg/Motiv/issues/168)
**Source:** bundle spec
[4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§4 Accessibility (18) — "wire `axe-core` into the existing Playwright suite", and §7's obligation that
it "passes on every `Motiv.Studio` view in CI".

## What this is

Three more coloured surfaces added to the accessibility sweep, in the state that colours them, and
whatever the sweep then reports fixed.

Spec 4J ([#167](https://github.com/karlssberg/Motiv/pull/167)) added one such state — the rules page
with a failed API call on screen — and the first scan of it found a 1.4.3 failure that had been
shipping since the banner was written. The ticket's argument is that the same pattern is used by
three surfaces the sweep still cannot reach, and that the honest way to find out whether they are
also wrong is to scan them rather than to do the arithmetic by hand against a guessed ground.

| Surface | Ground | Reached by |
|---|---|---|
| `.error` (`SchemaViolations`, `DocumentModal`, `RuleNodeEditor`) | pane / modal / inset | a document that fails validation |
| `.quarantine-badge` | explorer row, including the highlighted one | a proposition with a non-empty `quarantine` |
| `.dsl-popover-error` | popover panel | a payload the popover rejects |

## Global constraints

- **The sweep decides, not the arithmetic.** The ticket carries hand-measured ratios. They are a
  reason to look, not a finding — #167's lesson is precisely that nobody had measured the banner in
  place either. Nothing moves until axe reports it, and what moves is what axe named.
- **If contrast reports, move the token, not the class.** The same rule #167 applied to the light
  palette: quote the ratio against the *tightest* ground the colour is actually drawn on, in the
  token's own comment, so a later palette edit reads why the value is what it is.
- **No .NET.** The sweep serves the built SPA and answers the API from fixtures; it must stay that
  way, because that is what lets it run in the `ui` workflow on every pull request.
- **Fixture states live in `stubs.ts`.** They are the same kind of thing the existing constants are —
  the shape one endpoint returns, in one state — and a spec that inlined them would be asserting
  against a payload it authored two screens away from the fixture that says what the endpoint
  normally says.

## Sequence

1. **Baseline.** `pnpm -C ui install`, build `@motiv-rules/core` and `@motiv-rules/react` (the studio
   consumes their `dist`), then run the sweep unchanged so any later red is attributable.
2. **Three fixture-state helpers** in `e2e-a11y/stubs.ts`, each a route registered after the
   fixture's catch-all: a rejected document validation, a quarantined proposition, and a catalog
   whose spec carries *object* metadata — the popover has no rejection to render for a string
   payload, which is why the all-`String` fixture catalog cannot reach its error at all.
3. **A third group in `axe.spec.ts`**, `DANGER_SURFACES`, holding six states: the evaluate pane's
   schema violations (pane ground), the document modal's validation errors (modal ground), the
   builder row's own error (inset ground), the quarantine badge on an ordinary and on a *highlighted*
   explorer row (two grounds for one badge), and the payload popover refusing what was typed into it.
4. **Run it and read what comes back**, in both schemes. Fix each reported violation at its cause.
5. **Full sweep** — all three groups, both schemes, plus the keyboard suite — then the studio unit
   suite and a workspace typecheck.
6. **The conformance record**, if the sweep's scope changed what 1.4.3 can honestly claim.
   `docs/accessibility/vpat.md` is generated: regenerate rather than edit.
7. **The mandatory `code-simplifier` pass** over the diff.
8. **Plan and design in the same commit as the implementation**, per `CLAUDE.md`.

## What the sweep actually reported

Recorded here because it is the point of the slice, and because two of the three predictions were
wrong in an instructive direction:

- **`.error` — a 1.3.1 failure, not a contrast one.** `list (serious)` on both the schema-violation
  and validation-error lists: each `<li>` carried `role="alert"`, and a role *replaces* the element's
  own, so the `<ul>` was directly containing something that was no longer a list item. Contrast on
  those three grounds passed in both schemes. Fixed by moving the live-region role onto a wrapping
  `<div>`, which keeps the list a list and announces the group once rather than racing one alert per
  row.
- **`.dsl-popover-error` — clean.** Solid text on an opaque popover panel; no tint, no finding.
- **`.quarantine-badge` — the predicted 1.4.3 failure, and only on the highlighted row.** 4.83:1 on
  an ordinary row, 4.23:1 on the selected one, whose `--accent-weak` ground the ticket's arithmetic
  had not considered. Fixed by moving the dark palette's `--danger` from `#ff6d86` to `#ff8a9d`
  (4.87:1 on the tightest ground).

## Verification

- `pnpm --filter @motiv-rules/studio a11y` — 48 axe tests and the keyboard suite, both schemes.
- `pnpm -C ui -r test` and `pnpm -C ui -r typecheck`.
- `pnpm --filter @motiv-rules/studio a11y:report`, so `docs/accessibility/vpat.md` matches its source.
