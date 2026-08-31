# Spec 4J — The Failure Channel — Design

**Date:** 2026-08-31
**Status:** Shipped
**Source:** The boundary surface of bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§2 (ticket [07](https://github.com/karlssberg/Motiv/issues/107) — workflow is in-scope for the headless
packages) and §4 (ticket [18](https://github.com/karlssberg/Motiv/issues/118) — AA enforced in CI).
Closes [#150](https://github.com/karlssberg/Motiv/issues/150), a review finding scoped out of Spec 4B.
Tracked as [#170](https://github.com/karlssberg/Motiv/issues/170); shipped as
[#167](https://github.com/karlssberg/Motiv/pull/167).

> **Written after the merge.** This slice landed before `CLAUDE.md` carried the same-commit docs rule,
> and is one of the eleven the [#169](https://github.com/karlssberg/Motiv/issues/169) ledger marks as
> owed. The decisions below are recovered from the shipped diff and its review round, not proposed.

## Summary

Spec 4B promoted two workflow controllers into `@motiv-rules/core/workflow`, and they disagreed about
what happens when the server breaks. `PropositionWorkflowController` routes every thrown failure through
`describeUnexpectedFailure` into `state.failure`. `RuleWorkflowController` had no such field, so a
thrown `refresh`/`load`/`save` escaped to its caller: a consumer writing `void save()` got an unhandled
rejection and a page showing nothing — indistinguishable from the request never having been made.

That was faithful promotion, not a regression. The demo's `RuleHeader` never grew a failure banner, the
propositions page did, and 4B's proof of faithfulness was the demo suites passing unchanged. #150 exists
because faithfulness stops being the governing argument once both loops sit side by side in a *published*
surface: two controllers of the same shape with opposite error contracts is a wart an adopter trips on.

So `RuleWorkflowState` gains `failure: string | null`, Studio renders it, the markup both pages had
copied becomes one `ReportBanner` — and the axe sweep, pointed at that banner for the first time, failed
1.4.3 on a colour the palette had never been scanned against.

## Decisions (locked)

### 1. Two channels, because a 409 and a 500 are not the same event

`conflict: number | null` stays exactly as it was. It carries the version somebody else saved — a typed
refusal the API models, with a defined recovery (load again) and a sentence only the page can write,
because only the page knows the rule's name.

`failure: string | null` carries what the API *cannot* see: a 500, a 404, a body that will not parse.
These arrive as a thrown `RulesApiError`, and `describeUnexpectedFailure` — already in
`workflow/failureText.ts` for the proposition loop — is the whole rendering.

Collapsing them would mean either giving up the typed conflict (losing the version number the recovery
message needs) or writing conflicts into `failure` as text as well. The second is the one that looks
harmless: it would put one event in two banners saying different things about it.

### 2. One failure channel across three operations, cleared on the way *out*

The first round shipped this as three-ish: `load` cleared a standing failure when it started, `refresh`
never cleared one at all, and `save` cleared on its typed outcome. Copilot's review found the hole, and
it is worth recording because the fix is the actual design rather than a patch.

A `refresh` that succeeded replaced the listing while the banner went on saying the listing had failed —
precisely the "banner outlives the retry it triggered" defect `load` already had a test against. Seen
from the type it is the same hole: `failure` was documented as being "against the loaded rule", while
`refresh` reports listing failures with nothing loaded at all. One channel had two subjects and said so
nowhere.

So it is stated as one channel with one rule: **the last unexpected failure, from whichever of refresh,
load or save raised it, cleared by whichever runs next.** `#clearFailure` is that rule in one place,
called on the way out of all three.

- **Out, not back.** A report that outlives the act it triggered reads as that act having failed too. The
  clear happens as the new operation *starts*, so the banner is gone while the retry is in flight.
- `save` no longer clears on its typed outcome. That was the same clear a beat too late, and it left a
  stale banner standing over the PUT it was retrying.
- `#clearFailure` is silent when there is nothing to drop, so an operation that reports nothing notifies
  nothing and the snapshot stays identity-stable.

### 3. A failed load leaves `loaded` standing — deliberately unlike its sibling

`PropositionWorkflowController` nulls its selection on a failed load. This one does not, and the
asymmetry is intentional rather than an oversight the symmetry work missed.

The proposition controller writes its selection *before* the fetch, so a failure leaves a selection that
can have diverged from what is in the store. The rule controller writes `loaded` only *after* a load
lands, so what is still there is the rule whose document is genuinely in the store. Dropping it would
demote a loaded rule to a local draft over a transient 500 — the recovery would be worse than the fault.

### 4. Every report is gated on still being current

Each operation already carried a supersession counter (`#refreshOp`, `#loadOp`). Failures go through the
same gate as successes: a superseded refresh's failure never lands over a newer listing, a superseded
load's never lands on a newer pick, and a save's is recorded only while the load it was aimed at is
still current. A banner about a rule no longer on screen is false of the one that is.

### 5. One banner component, because a second copy is a second chance to stop being an `alert`

`RuleHeader` and `PropositionsPage` had each open-coded the same `<div role="alert" class="conflict-banner">`
with an optional *Reload latest* button. That is two places for the `role="alert"` to be dropped, and a
report nobody hears is the defect the banner exists to fix.

`shell/ReportBanner.tsx` is the one implementation, taking an optional `onReload`: omitted where the page
has no identity to reload, so the recovery offer never appears as a button that does nothing. The CSS
class is renamed `.report-banner` to match what it now carries.

On `RuleHeader` the failure banner is rendered *above* the conflict banner, because it is always the
newer event: only a save records a conflict, and every operation clears the failure on its way out, so a
failure standing beside a conflict was necessarily raised after it.

### 6. The axe sweep gains a view only a broken server produces

Spec 4D's sweep visits every view and every hard surface. It could not visit this one: the banner is
reachable only when the API fails, so no route-only sweep would ever have scanned it — on *either* page,
for the whole life of the component.

The sweep now breaks the listing endpoint (a routed 503, registered after the fixture's catch-all so it
wins) and scans `/#/rules` in the state that produces. One scan covers both pages, since it is the same
component.

The first run of it failed 1.4.3 for real. `--danger` as banner text on its own 12% tint reads against
whatever is behind the translucent fill — on the canvas, 3.3:1, not the 4.5:1 the page claims to enforce.
`--danger` moves `#d1435b` → `#a8293f`: 4.9:1 there, 4.7:1 on the 15% tint `.quarantine-badge` uses. Every
other site reads it as solid text or a button fill, where darkening only widens the margin. The dark
scheme's `#ff6d86` is unchanged — it clears on its own tint already.

This is the same treatment `--faint` and the DSL pills got in 4D, and the token comment is restated to
say what all four ratios are quoted against: the *tightest* ground the colour is actually drawn on, which
for a translucent tint is the darkest surface beneath it.

## What this does not do

- **It does not unify the two controllers.** They still differ in decision 3, and in that the proposition
  loop renders conflicts and refusals as text where this one has typed channels for them. The slice makes
  the error *contract* the same; the loops stay two loops.
- **It does not add a failure channel to `RuleEditorStore`.** Document validation errors keep their own
  list; `failure` is about the transport, not the document.
- **It does not retry.** The banner offers *Reload latest* where there is an identity to reload, and
  nothing where there is not. Automatic retry is a policy an adopter should own.
- **It does not close the manual half of §4's enforcement** ([#172](https://github.com/karlssberg/Motiv/issues/172)).
  Announcement quality for a banner that appears without focus moving is exactly what axe cannot judge.

## Verification obligations

- A thrown `refresh`, `load` or `save` is reported in `failure` rather than escaping the caller.
- The channel is one channel: a successful operation of *any* of the three clears what a previous one
  raised, and clears it before its own request lands.
- A typed conflict is not written into `failure`.
- A superseded operation's failure never lands — asserted for refresh, load and save independently.
- A failed load leaves `loaded` standing.
- `ReportBanner` is an `alert`, offers no button when given no `onReload`, and runs the one it is given.
- `axe-core` passes on the rules page in its API-failure state, in both colour schemes.

## Outcome (recorded after the build)

Shipped as [#167](https://github.com/karlssberg/Motiv/pull/167) across 11 files: `+391 / −34`.

**Ten new controller tests** in `workflow-rule.test.ts`, three new component tests in
`ReportBanner.test.tsx`, and two in `RuleHeader.test.tsx`.

**One test was passing vacuously.** The case claiming a typed outcome did the failure-clearing had a load
in its own arrangement that had already cleared it. Rewritten to exercise `save`, where the claim is
actually testable.

**The contrast failure was found, not predicted.** Decision 6's scan is the only reason `--danger` moved;
the banner had looked that way on both pages since it was written.

**Not run:** the .NET suite and `pnpm e2e` (which drives the .NET host) — no .NET SDK was available in
that session ([#173](https://github.com/karlssberg/Motiv/issues/173)), and no C# is touched.
