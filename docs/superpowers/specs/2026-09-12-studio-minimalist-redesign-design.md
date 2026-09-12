# Studio redesign: one surface, the document titled where it is edited

**Date:** 2026-09-12
**Status:** Implemented

## Problem

Studio worked, and read as cluttered. Three things compounded:

1. **Three layers of surface before any content.** A grey canvas behind the columns, a white pane
   on it, an off-white header on the pane, and a bordered, filled, rounded card for every builder
   row. Each was a reasonable choice alone; stacked, the eye parsed boxes inside boxes before it
   reached the rule.
2. **Two kinds of thing on one bar.** The app bar carried the brand and the page switch (*where am
   I in the app*) beside a breadcrumb trail, a model pill and a version note (*what am I
   editing*), and the two competed for one row — worst on the propositions page, where a dotted
   name became a four-segment trail.
3. **Chrome for what did not exist.** A disabled "parameters — coming" button sat in the editor's
   header at rest, and the row controls were a mix of unicode glyphs (`▸ ▾ ⋯ ◈ ＋ 📌`) that render
   differently on every platform — the very reason the toolbar had already moved to SVG.

The brief: slicker and more professional, more minimalist, without losing functionality, and
mindful of the usual usability heuristics.

## Decisions

| Question | Decision |
|---|---|
| Surfaces | **One.** `--sh-canvas`, `--sh-appbar`, `--sh-panel` and `--sh-panel-head` resolve to the one background; panes divide by hairline. `--sh-inset` is the only tint, for wells that hold content. Token *names* are kept so no site moves. |
| Where the document is named | **The editor pane's header.** A new `DocumentTitle` (name, model pill, version, optional note) is passed to `EditorPane` through a `title` slot. The top bar keeps brand, pages and actions. |
| Who owns the rule workflow | **`RulesPage`.** Two panes now need what it knows, so `useRuleWorkflow` moves up from `RuleHeader`, which becomes the top bar and its banners. `PropositionsPage` already owned its workflow. |
| Save | **A primary, worded button.** `ToolbarAction` gains `emphasis: 'primary'`; the label is visible text, not an `aria-label`, so name and label cannot drift (WCAG 2.5.3). Open and JSON stay ghost glyphs. |
| Checkout | **In a rail beside the editor, under Evaluate**, not a full-width footer. Both say their result the same way: a `Verdict` chip (check or cross, then the word) beside the button, then the assertions with a mark each. |
| Builder rows | **Lines, not cards.** Structure is the indent guide `.node-kids` draws; hover is a fill; selection is a bar and a tint. Row controls are the shell's SVG set; carets are `Caret`, whose state is the button's `aria-expanded`. |
| Palette footer | **Labelled.** `Toolbar` gains `labelled`, so New / Derive / Override / Delete wear their words — recognition where a tooltip is recall — with the arrow and Enter keys shown beside them. |
| Placeholders | The header's "parameters — coming" is removed. The detail panel's "expression — coming" stays: it is inside a disclosure, not in the chrome at rest. |
| DSL filename | The loaded document's name, `draft.motiv` for a nameless draft — not the `quota-rule.motiv` constant. |
| Propositions with nothing open | **An empty state, not the panes.** The editor store is shared with the rules page, so with no selection it still holds that page's last document; showing it under "no proposition open" was a contradiction the old breadcrumb merely hid. The page now says what to do (Choose / New) and the JSON action is unavailable until something is open. |
| Colour | Every value is an existing token; `--ok`, `--warn` and `--on-accent` are added (the first two are `--dsl-ok`/`--dsl-warn`'s values under a name the panes can share). |
| Rules with nothing open | **The same empty state, and the route as the selection.** The rule name moves from workflow state into the hash (`#/rules/<name>`), so a deep link works, the page switch round-trips through `lastSelection`, and Close is a navigation to the bare page. The local draft is retired: a document with nothing to save it back to was the source of the stale-document confusion, and nothing could be done with it. |
| Close and discard | **One ghost Close; a dialog when dirty.** Discard is the shell's one destructive act, so it is never one click from the action beside it: Close on a dirty document asks (Keep editing / Save & close / Discard changes), with the safe answer first and focused. |
| Save variants | **A split button.** After GitHub's "Close issue": the filled button wears the variant in force, a chevron opens a radio menu of Save / Save & close with each one's consequence. Choosing a variant runs it and makes it the default, remembered per browser — re-arming alone is the part of GitHub's control people trip on. |
| Dirty | **A core flag, not a UI guess.** `RuleEditorStore` keeps a baseline the workflows set on load and save; `dirty` is a structural comparison against it, so a DSL edit typed back to what was loaded is clean. `loadDocument` does not move the baseline, because the DSL sync uses it to commit text edits. |

Two things the canvas proposed and this slice deliberately does **not** ship:

- **An "Unsaved changes" indicator.** The dirty flag it needs now exists in core (see *Dirty*
  above, added when Close and Save & close needed it); the indicator itself is still a follow-up.
- **The rule name as the picker trigger.** The toolbar's Open button stays: it is the affordance
  every test and every user knows, and a title that is secretly a button would need an
  `aria-label` that disagrees with its visible text.

## What did not change

Every accessible name and role. `Open`, `Save`, `JSON`, `Evaluate`, `Try checkout`, the page
links, the row carets' labels, the palette's dialog names, the `sync status` pill and its exact
text. The e2e and a11y suites pin these, and the redesign was done under them: 435 unit tests, 48
axe scans in both schemes and 28 end-to-end runs pass, with two intentional test edits (the caret
is an SVG with `aria-expanded` rather than a `▸`/`▾` character; the checkout verdict class is
`.rule-verdict`) and one relocation (`RuleHeader.test` becomes `RulesPage.test`, since the page
owns the workflow).

## Heuristics, mapped

- *Visibility of system status* — verdict chips beside the button that produced them; the sync
  pill unchanged; the version beside the name.
- *Recognition over recall* — labelled palette actions, `⌘K` beside the title, key hints in the
  palette footer.
- *Consistency* — one 28px control height, one SVG glyph set, one result layout for draft and live.
- *Error prevention* — the blast-radius strip keeps its place under the bar, now with the affected
  documents as tags.
- *Aesthetic and minimalist design* — placeholders out of the chrome, controls resting hidden,
  one surface.
- *User control* — Reload latest on conflicts, unchanged; the palette says `esc`.

## Out of scope

- A dirty indicator (above).
- Studio's `README.md` screenshots — there are none to update.
- The `docs/accessibility/vpat.md` report: no criterion's verdict or evidence kind changed, so it
  is not regenerated.
