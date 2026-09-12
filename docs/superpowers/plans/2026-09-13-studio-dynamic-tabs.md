# Studio dynamic tabs — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax.
> Written in the same change as the implementation, per `CLAUDE.md`.

**Goal:** Replace Studio's two static pages with one shell in which any number of rules and
propositions are open at once, each in its own tab with its own draft, and a rule tab knows when a
proposition it uses has been edited or saved in another.

**Architecture:** A `Workspace` store (tabs, one `RuleEditorStore` per tab, latest versions) beside
the hash route, which stays the truth for the *active* document. The page components become per-tab
documents kept mounted and hidden, so switching never reloads. The app bar carries the tab strip;
document actions move into the editor pane's header.

**Tech stack:** React 18, `@motiv-rules/core` / `@motiv-rules/react` workflows, Vitest + Testing
Library, Playwright (e2e and axe sweep), plain CSS on the existing tokens.

**Spec:** `docs/superpowers/specs/2026-09-13-studio-dynamic-tabs-design.md`

## Global constraints

- Every accessible name the e2e and axe suites pin is either kept or changed in the suite in the
  same task — `Editor`, `Evaluate`, `Checkout` regions; `Open`, `JSON`, `Save`, `Save options`
  buttons; dialogs `Propositions` (the explorer) and the unsaved-changes dialog.
- No new runtime dependency in `ui/`.
- `pnpm -C ui/apps/studio a11y` must stay green; an `aria-controls` / `aria-activedescendant` IDREF
  is dropped whenever its target is unmounted.
- Text colour ratios: every new text colour is an existing token drawn on a ground it already
  clears.

## Tasks

- [ ] **1. Workspace store.** `src/shell/workspace.ts`: `Workspace` with `getState`/`subscribe`,
  `open(kind, name)` (idempotent; creates a `RuleEditorStore` and remembers the tab in
  `sessionStorage` under `motiv.studio.tabs`), `activate`, `close` (neighbour to the right, else
  left, becomes active), `noteSaved(kind, name, version)`, `acknowledge(id)`, `setLatest(entries)`;
  `referencesOf(document)` and `referenceStatuses(state, doc, refs)` from the prototype;
  `restore()` reads the remembered list. Tests: `test/shell/workspace.test.ts` — open is
  idempotent and activates; close picks the neighbour; a store per tab; noteSaved bumps `latest`
  and re-baselines the saver's `seenAt`; references flagged `changedSinceSeen` only after a save
  newer than `seenAt`, `editingElsewhere` only while another tab's store is dirty; restore
  round-trips through storage and tolerates a denied storage.
- [ ] **2. Per-tab documents.** `RulesPage` → `src/panes/RuleDocument.tsx`, `PropositionsPage` →
  `src/panes/PropositionDocument.tsx`. Each takes `{ client, tab, workspace, active, onClose }`,
  wraps its body in `RuleEditorProvider store={tab.store}`, keeps the workflow hook, banners,
  editor + rail, `DocumentModal`, and a `DocActions` toolbar (JSON, split Save whose *Save & close*
  closes the tab) passed through `EditorPane`'s `actions`. `RuleDocument` renders the
  `ReferencesStrip` above the body. The palettes and the app bar leave. Tests: the two page test
  files become `RuleDocument.test.tsx` / `PropositionDocument.test.tsx`, keeping every workflow
  case (load, save, conflict, failure, dependents, code-default note, Save & close closes the
  tab, discard question) and dropping the palette, route and app-bar cases, which move to task 5.
- [ ] **3. Tab strip.** `src/shell/TabStrip.tsx` (chips, overflow menu, compact dropdown, `+`,
  active-tab swap), `src/shell/TabCard.tsx` (kind tile / glyph, `splitName`, `useHoverCard`,
  `TabHoverCard`), `src/shell/tabEvents.ts`. Chips are `role="tab"` in a `tablist` labelled
  *Open documents*; the active chip has `aria-selected` and `tabIndex=0`. Tests:
  `test/shell/TabStrip.test.tsx` — one chip per tab with its kind; the active one is selected;
  clicking activates; × asks `onRequestClose`; middle-click and Delete close; ←/→ move; the card
  appears on focus with the full name and `aria-describedby` points at it; more tabs than fit go to
  the "+N" menu and choosing one activates it; the dropdown renders under 640px.
- [ ] **4. Open palette.** `src/shell/OpenPalette.tsx`: rules and propositions in one
  `CommandPalette` named *Open*, rows carry kind and an *open* badge, footer *Manage propositions*
  hands over to the explorer. Tests: `test/shell/OpenPalette.test.tsx`.
- [ ] **5. Shell and routing.** `src/shell/WorkspaceShell.tsx` owns the workspace, the shell-level
  proposition workflow (listing, create, remove) for the explorer, the palettes, ⌘K / ⌘W, the
  close question, and one hidden `TabPanel` per tab. `App` maps the route onto it: a named route
  opens + activates; activation navigates; closing the active tab navigates to the neighbour or
  the bare page. `AppBar` drops the page nav for the strip and keeps the Admin link.
  `DocumentActions` and `lastSelection` are deleted with their tests. Tests: `App.test.tsx` (deep
  link opens a tab; two routes are two tabs; closing navigates; admin route), `AppBar.test.tsx`
  (Admin link cases kept; nav cases replaced by "carries the strip"), `WorkspaceShell.test.tsx`
  (⌘K opens *Open*; picking opens a tab; explorer create/delete refresh the listing; a
  proposition save in one tab marks the reference *updated* in a rule tab).
- [ ] **6. Stylesheet.** Strip, chips, tiles, dropdown, overflow menu, hover card, references
  strip, `.pane-actions`, compact bar wrap — moved from `prototype.css` into `app.css` under the
  shell sections, then `src/prototype/` is deleted and the `?variant=` gate leaves `App`.
- [ ] **7. Suites.** e2e `shell.ts`: `openPalette('Rules')` opens *Open*; `openPalette('Propositions')`
  opens *Open* then *Manage propositions*; `closing.spec` closes via the chip; `propositions.spec`
  page-switch cases become tab cases; axe sweep `visit` waits for the strip; keyboard suite's link
  cases become chip cases; conformance records regenerated. `useDocumentTitle` names the active
  document.
- [ ] **8. Docs and review.** README *Layout* / *Panes*; `docs/` if a user-facing page describes
  the shell; code-simplifier pass; full `pnpm -C ui/apps/studio typecheck`, `test`, `e2e`, `a11y`.
