# Spec 4J follow-up — The other `--danger` surfaces — Design

**Date:** 2026-09-09
**Ticket:** [#168](https://github.com/karlssberg/Motiv/issues/168)
**Plan:** [`2026-09-09-spec-4j-followup-danger-surfaces.md`](../plans/2026-09-09-spec-4j-followup-danger-surfaces.md)
**Source:** bundle spec
[4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§4 Accessibility (18) and §7's "`axe-core` passes on every `Motiv.Studio` view in CI".
**Lineage:** Spec 4D, The Accessibility Slice ([#155](https://github.com/karlssberg/Motiv/pull/155)) →
4H, The Conformance Report ([#164](https://github.com/karlssberg/Motiv/pull/164)) →
4J, The Failure Channel ([#167](https://github.com/karlssberg/Motiv/pull/167)) → here.

## The defect is in the sweep's reach, not in a stylesheet

A colour is not a contrast ratio. `.report-banner`, `.error` and `.quarantine-badge` all draw
`--danger` text on `color-mix(in srgb, var(--danger) 12%, transparent)` — a **translucent** tint,
which has no lightness of its own and borrows it from whatever is painted beneath. The same token is
therefore a different ratio on the canvas, on a pane, in a modal, on an inset, and on a highlighted
row, and no inspection of the token or of the class tells you which of those clears 4.5:1.

Only a scan of the composited pixels does. And a scan can only reach pixels that exist — which is
what makes this a *reach* problem rather than a palette one:

> A state no view produces contributes no nodes to a scan of the page behind it.

Spec 4J is the proof. It added exactly one such state — the rules page with a failed API call — and
the first scan of it reported 3.3:1 on a banner that had been shipping since it was written. The
banner was not new; the *state* was. Ticket #168 is the observation that three more surfaces use the
same pattern and that the sweep still cannot reach any of them.

### Why the ticket's own arithmetic was not the answer

#168 carries hand-measured figures: `#ff6d86` at 4.45:1 on a 12% tint and 4.23:1 at 15%, both over
`--sh-inset`. Those numbers are right, and they were still the wrong basis for a fix, for two
reasons the slice then demonstrated:

1. **They guessed the ground.** `--sh-inset` is not where the quarantine badge is drawn. The badge
   sits on an explorer row — `--sh-panel`, where the same 15% tint is 4.83:1 and *passes* — except
   when that row is the selected one, which paints itself `--accent-weak` (`#222a44`) and takes the
   ratio to 4.23:1. The failing ground was one the arithmetic had not enumerated, and it arrived at
   the same number by coincidence.
2. **They would have found nothing on two of the three surfaces.** `.error` passed on all three of
   its grounds in both schemes. `.dsl-popover-error` is solid text on an opaque panel and was never
   at risk. A palette move justified by hand arithmetic would have moved a token for surfaces that
   did not need it, and left the one that did — the highlighted row — untouched.

This is the same lesson #167 taught, stated the other way round: the sweep exists to replace
arithmetic against a guessed ground, so the fix has to wait for the sweep.

## What was built

### A third axis of coverage

`axe.spec.ts` scanned on two axes — every view, and every hard surface in the state it is hard in.
Neither reaches a rejection. Nothing the app does *when it is working* produces a schema violation, a
failed validation, a quarantined proposition or a refused payload, so a suite that visited every
route and opened every surface still scanned none of them.

`DANGER_SURFACES` is that third axis, and it is a separate group rather than more `VIEWS` because
what makes its members unreachable is one shared cause: the state is a rejection. Six of them, to
cover four distinct grounds for `.error` and `.quarantine-badge`:

| Surface | Ground it exercises |
|---|---|
| evaluate pane, model refused by the catalog schema | `--sh-panel` |
| document modal, validation rejected | modal surface |
| builder row, carrying the error validation returned for it | inset |
| explorer, listing a quarantined proposition | `--sh-panel` row |
| explorer, quarantined proposition on the **highlighted** row | `--accent-weak` |
| payload popover, refusing the JSON typed into it | popover panel |

The last of these needs a fixture the suite did not have. `PayloadPopover` only *has* a rejection to
render when the spec's metadata is an object — a string payload is taken verbatim and cannot fail to
parse — and the fixture catalog is all `String`. `withObjectMetadata` moves one spec off it. This is
the general shape of the three new helpers: each is a route registered after the fixture's catch-all
(Playwright resolves matching handlers most-recent-first), and each lives in `stubs.ts` beside the
constants it varies rather than inline in the spec.

### The 1.3.1 defect the `.error` states turned up

Not the one anyone was looking for, and the more interesting of the two:

```
list (serious): <ul> and <ol> must only directly contain <li>, <script> or <template> elements
```

Both error lists rendered `<li role="alert">`. The intent was sound and was written down —
a list that arrives after a button was pressed has to announce itself rather than sit there silently
— but **a role replaces the element's own**. An `<li>` that is an alert is not a list item, so the
`<ul>` was directly containing something that was not one, and the list semantics the `aria-label`
promised were not there.

The fix moves the live region onto a wrapping `<div role="alert">`. It keeps both properties instead
of trading one for the other, and it reads better besides: one alert naming every violation, rather
than one per row racing the others into the same live region.

That this was found at all is the argument for the axis. The defect is not about colour, was not
predicted by the ticket, and is in markup that had been reviewed — it was simply never scanned,
because the state that renders it had never been produced.

### The palette move

`--danger`, dark scheme: `#ff6d86` → `#ff8a9d`.

| Ground | 15% tint | 12% tint | solid |
|---|---|---|---|
| `--accent-weak` — highlighted explorer row | **4.87** (was 4.23) | 5.10 | 6.32 |
| `--sh-panel` — ordinary explorer row | 5.59 | 5.93 | 7.34 |
| `--sh-inset` | 4.89 | 5.13 | 6.40 |
| `--sh-canvas` | 6.29 | 6.70 | 8.14 |

The light palette is unchanged: #167 already moved it to `#a8293f`, and every new surface passed in
that scheme. `.btn-danger` — the one site where `--danger` is a fill rather than text — improves
rather than degrades, since its label is `--bg` and the fill got lighter (8.0:1, and 5.9:1 on the
darkened hover fill).

The token's comment quotes the ratio against the tightest ground *and names which ground that is*,
because the answer flips with the scheme: in the light palette the tightest ground is the darkest
surface beneath the tint, and in the dark palette it is the lightest. The file header now says so.

## What this does not do

- **It does not make the sweep exhaustive.** Every surface here was reachable because someone named
  it. The general problem — a state that exists but that no test has thought to produce — is not
  solved by adding six more states, and the honest claim in the conformance record is the one that
  is now written there: enforced on every view, every open surface, and every state a rejection
  produces.
- **It does not touch the manual pass.** Colour contrast is squarely in axe's half of AA. Ticket
  [#172](https://github.com/karlssberg/Motiv/issues/172) — the screen-reader audit — remains a human's,
  and the 1.3.1 fix above is the kind of thing a screen-reader pass would also have caught, which is
  the case for keeping both.
- **It does not revisit `role="alert"` on the singular error spans.** `RuleNodeEditor` and
  `PendingSlot` render a lone `<span role="alert">` with no list around it. That is correct as it
  stands and axe agrees; only the two `<ul>`-wrapped lists had the collision.
