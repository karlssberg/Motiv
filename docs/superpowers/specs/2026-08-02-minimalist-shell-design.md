# Minimalist shell: icon toolbar, command palette, modal document viewer

**Date:** 2026-08-02
**Status:** Approved, ready for planning

## Problem

The demo shell has grown wide. Both pages render four columns side by side, and the Propositions
page spends one of them on a tree explorer that is only consulted when changing which proposition is
being edited. Every action is a worded button, so the chrome competes with the content it frames.

The goal is a minimalist shell: fewer persistent surfaces, icons instead of words for operations,
and the two browsing surfaces — the proposition explorer and the JSON document view — moved behind
modals opened from a toolbar.

## Decisions

| Question | Decision |
|---|---|
| Scope | Both pages share one toolbar. `AppBar` is already shared; letting the two chromes diverge is what extracting it from `RuleHeader` was meant to prevent. |
| Toolbar treatment | **Ghost** — no background or border at rest, a soft inset well on hover and focus. |
| Toolbar contents | Three actions only: **Open**, **Save**, **JSON**. |
| New / Derive / Override / Delete | Inside the explorer modal, not the toolbar. Three of the four act on a selection, and the modal is the only place a selection is visible. |
| Explorer presentation | **Command palette** — floats high, opens with the cursor in search, tree renders as filtered results. |
| Palette state on reopen | **Always fresh.** Empty search, nothing expanded. |
| JSON view | Modal on **both** pages, opened from the toolbar. Content unchanged from today's `JsonPane` — the pane becomes a modal rather than going away, so nothing is lost and both pages gain the column. |
| Navigation | Keeps its words (`Rules`, `Propositions`), each prefixed with a small glyph. |
| Icons | Inline SVG, hand-authored. No icon package. |
| Modal mechanism | Native `<dialog>` + `showModal()`. |

The palette choice and the reopen-state choice are one decision, not two. A palette that reopens
holding the previous query is annoying, while a persistent-feeling tree that forgets its expanded
nodes reads as broken. Choosing the palette is choosing "always fresh".

## Architecture

### New — `ui/apps/demo/src/shell/`

**`icons.tsx`** — the inline SVG set. One export per glyph, each a function returning an `<svg>`
with `stroke="currentColor"` and no fixed colour, so every icon inherits from its button.

`ui/` carries a zero-new-runtime-dependency constraint that held across the whole runtime-propositions
branch, so an icon package is out. Unicode glyphs were rejected separately: they render
inconsistently enough across platforms that a toolbar built from them looks broken on someone
else's machine.

**`Modal.tsx`** — a wrapper over the native `<dialog>` element. Calls `showModal()` on mount and
`close()` on unmount, renders `children` inside, and reports dismissal through one `onClose`
callback regardless of whether it came from Escape, the close control, or a backdrop click.

Native `<dialog>` gives focus trapping, Escape handling, backdrop inertness and correct assistive-
technology semantics with no library and no hand-rolled focus management. The existing
`PropositionDialog` sets `aria-modal="true"` with none of those behaviours — a recorded defect, since
`aria-modal` instructs AT to hide everything outside the dialog while focus is left on the button
that opened it. Introducing `Modal` for the two new surfaces and migrating `PropositionDialog` onto
it closes that defect as a consequence rather than as separate work.

**`Toolbar.tsx`** — renders an icon button per action from a declarative list. Each button carries
both `aria-label` (the accessible name) and `title` (the sighted tooltip): a bare glyph teaches
nothing on first encounter, and a tooltip is the only affordance that explains it.

**`CommandPalette.tsx`** — a `Modal` containing a search input and a result list, plus a footer
action strip supplied by the caller. Owns highlight position and query; owns no domain knowledge.
The caller supplies the items and renders each row.

### Changed

- **`AppBar.tsx`** — nav items gain glyph prefixes; the controls slot receives the `Toolbar`.
- **`PropositionExplorer.tsx`** — loses its own chrome (heading, surrounding rail) and becomes the
  palette's proposition contents: the namespace tree plus the four actions.
- **`PropositionsPage.tsx`** — owns palette and JSON-modal open state; drops the explorer and
  `JsonPane` from `shell-body`.
- **`RuleHeader.tsx`** — the breadcrumb `ListboxPicker` is replaced by the palette. The breadcrumb
  keeps naming the current rule, as text rather than a dropdown trigger.
- **`RulesPage.tsx`** — drops `JsonPane` from `shell-body`.
- **`PropositionDialog.tsx`** — migrates onto `Modal`.
- **`app.css`** — ghost button styles, palette layout, the mobile fullscreen rule.

### Layout

Both pages go from four columns to two:

| Page | Before | After |
|---|---|---|
| Propositions | explorer · Editor · JSON · Evaluate | **Editor · Evaluate** |
| Rules | Editor · JSON · Evaluate (+ Checkout below) | **Editor · Evaluate** (+ Checkout below) |

### Mobile

Below the existing 900px breakpoint the palette and the JSON modal are fullscreen: edge to edge, no
visible scrim, their own close control. The breakpoint is reused rather than introduced — `app.css`
already stacks `shell-body` there.

## Interaction

**Opening.** The Open icon and `⌘K` / `Ctrl+K` both open the palette. It appears with the search
input focused and empty.

**Keyboard.** `↑` / `↓` move the highlight, `Enter` loads the highlighted item and closes, `Esc`
closes without loading. The highlight moves via `aria-activedescendant`, so focus stays in the
search input and typing never requires a return trip.

**Actions.** New needs no selection. Derive, Override and Delete act on the highlighted row and are
unavailable without one — unavailable in the same way Save is, via `aria-disabled` and a no-op
handler, so the reason stays reachable. Delete sits apart from the other three: it is the only one
that destroys something, and placing it beside Derive makes a mis-click cheap.

The footer strip is supplied by the caller, so it is present on the Propositions page and absent on
the Rules page. Rules are compile-time placeholders — there is nothing to create, derive or delete —
so the Rules palette is a chooser and nothing more.

**Save.** Disabled whenever the loaded proposition has `version === 0`, which is the contract's
"purely compiled": no overlay document exists for a `PUT` to update.

Save uses `aria-disabled="true"` with a no-op handler rather than the `disabled` attribute. A
`disabled` button leaves the tab order in every major browser, so a Tab-navigating screen-reader user
never reaches it and never hears why it is unavailable. This is not a special case for Save — it is
the correction of a defect the whole-branch review found in `PropositionDialog`, where an
`aria-describedby` explaining a disabled Create button can only be heard in browse mode. The new
pattern is the correct one, and `PropositionDialog` adopts it during its migration.

## Testing

### The jsdom constraint

**jsdom 25.0.1 defines `HTMLDialogElement` but does not implement `showModal()`.** Calling it throws
`TypeError: showModal is not a function`. Verified directly against the installed version, not
assumed.

A ~10-line shim in the vitest setup lets components render: it sets `open = true` on `showModal()`
and clears it on `close()`. That is all it can honestly do — jsdom has no top layer, so there is no
focus trap, no inertness, and no Escape handling to observe.

**Therefore: focus trap, Escape dismissal and backdrop inertness are Playwright assertions only, and
unit tests must not claim to cover them.** A unit test asserting "focus moved into the dialog" under
the shim would pass or fail for reasons unrelated to production behaviour — which is precisely the
class of test this codebase has produced nine times, six of them caught by mutation testing rather
than by review. The shim's limits belong in a comment beside it.

### Discipline

Every new guard is verified by mutation: apply the mutation, confirm the test fails, revert, confirm
it passes. A test that has not been mutation-checked is not evidence that the guard works.

### End-to-end

Three specs drive surfaces this replaces — `propositions.spec.ts` and `live-rules.spec.ts` drive the
explorer rail and the breadcrumb rule picker, and `dsl.spec.ts` reaches the panes being moved. These
are rewritten against the new shell, not patched. The e2e churn is the largest single cost of this
change and should be planned as its own task rather than absorbed into the others.

## Out of scope

- **`AppBar`'s `role="tablist"` → `<nav>` + anchors.** Still the correct markup, and it would give
  middle-click and open-in-new-tab for free under hash routing. Excluded because it is a behaviour
  change, and running it inside a visual redesign would entangle two independent risks.
- **Pane behaviour.** Editor, Evaluate and Checkout are relocated, not modified.
- **New / Derive / Override for rules.** Rules are placeholders for compile-time logic; only
  propositions are authored.
- **Roving tabindex in the tree.** Pre-existing house style; `EditorPane` does not have it either.

## Risks

| Risk | Mitigation |
|---|---|
| e2e rewrite is large and touches specs unrelated to this feature | Its own task, done last, against a shell that has stopped moving |
| `<dialog>` untestable at unit level for its main benefit | Shim for rendering; behaviour proven in Playwright; the gap stated in the spec and in a comment |
| Icon-only actions are undiscoverable on first use | `aria-label` + `title` on every one; navigation deliberately keeps its words |
| Branch already has an open PR | This work lands on its own branch off that one, so the PR stays reviewable |

## Success criteria

1. Both pages show a three-icon ghost toolbar; no worded operation buttons remain in the chrome.
2. The palette opens from the icon and from `⌘K`, fresh each time, and is fully keyboard-operable.
3. The explorer rail and the JSON pane no longer occupy shell columns on either page.
4. Below 900px the palette and JSON modal are fullscreen.
5. `PropositionDialog` renders through `Modal`, and its `aria-modal`-without-focus-trap defect is
   closed.
6. Zero new runtime dependencies in `ui/`.
7. Full suite green: .NET, all three UI packages, typechecks, and e2e.
