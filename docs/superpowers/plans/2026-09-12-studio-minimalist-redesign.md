# Studio redesign — implementation plan

> Written in the same change as the implementation, per `CLAUDE.md`. Design:
> `docs/superpowers/specs/2026-09-12-studio-minimalist-redesign-design.md`. The visual reference is
> the design canvas the user approved (five artboards: Rules light and dark, Propositions with the
> palette open, a component sheet, a phone-width stack).

**Goal:** Restyle Motiv Studio to one surface with the document titled in the editor, keeping every
accessible name the suites pin.

**Approach:** Structure first, stylesheet last. Each structural slice starts from a failing unit
test; the stylesheet is checked by the axe sweep and by driving the built app in a browser at
1440px and 390px in both colour schemes.

## Tasks

- [x] **1. Shell structure.** `RulesPage` owns `useRuleWorkflow`; `RuleHeader` takes `workflow` and
  renders the bar, banners, palette and modal. `EditorPane` gains `title` and drops the parameters
  placeholder. New `DocumentTitle`. `Toolbar` gains `emphasis: 'primary'` and `labelled`.
  Tests: `RuleHeader.test` → `RulesPage.test` (title asserted inside the Editor region, absent from
  the banner); `PropositionsPage.test` scopes the breadcrumb to the editor; `EditorPane.test`
  asserts the title slot leads the header; `Toolbar.test` covers both new variants;
  `ExtensionPoints.test` asserts the placeholder is gone from the chrome.
- [x] **2. Rail.** `RulesPage` lays out `EditorPane` beside a `.rail` of `EvaluatePane` and
  `CheckoutPane`; `PropositionsPage` the same with Evaluate alone. New `Verdict` chip; checkout
  verdicts become `.rule-verdict` with a kind badge and per-assertion marks; pane hints move out of
  the `<h2>` so heading names stay one word. The e2e locator for the screening verdict follows the
  class rename.
- [x] **3. Glyphs.** `icons.tsx` gains `Caret`, `IconMore`, `IconPin`, `IconSelect`, `IconCheck`,
  `IconCross`, `IconPlay`, `IconWarn`, `IconRefresh`. Builder rows, the pending slot, the node menu,
  the report banner and the dependents strip use them. `RuleNodeEditor.test` asserts the caret's
  `aria-expanded` and hidden SVG instead of a character.
- [x] **4. Palette footer.** `PropositionExplorer` renders its `Toolbar` labelled with key hints.
- [x] **5. Stylesheet.** `tokens.css`: one surface, `--ok`/`--warn`/`--on-accent`, `--shadow`.
  `app.css`: rewritten to the canvas — 48px bars, underline nav, 28px controls, line rows with
  indent guides, rail, verdicts, palette, narrow-viewport block last so it wins the cascade.
- [x] **6. Verify.** `pnpm typecheck`, `pnpm test` (435), `pnpm a11y` (48), `MOTIV_E2E_PORT=5188
  pnpm e2e` (28 passed, 8 auth skipped). Driven in the browser against this worktree's own host on
  a spare port — the host another checkout left on 5100 answers 500 to the Vite proxy. One e2e was
  hardened on the way: the ⌘K test pressed the chord straight after `goto`, racing the effect that
  binds it; it now waits for the page chrome first, as the a11y suite's `visit` already did.
- [x] **6b. Propositions empty state.** With nothing selected the page renders `.empty-state`
  (Choose a proposition / New proposition) instead of the editor and rail, and the toolbar's JSON
  action is unavailable — the shared store's document is the rules page's, not this page's.
- [x] **6c. Selection survives the page switch.** `shell/lastSelection.ts` keeps each page's last
  selection in `sessionStorage`; `AppBar` mints the Propositions link with it, and the page records
  every change (including deselection). Rules → Propositions → back lands where the user left off.
- [x] **7. Docs.** Studio `README.md` layout section; this plan and the design doc.
- [x] **8. Dirty flag in core.** `RuleEditorStore` gains a baseline, `markClean()` and
  `EditorState.dirty` (structural compare, memoised per document). `loadDocument` does *not* move
  the baseline — the DSL sync calls it to commit a text edit — so both workflow controllers call
  `markClean()` after a load lands and after a save succeeds. `save()` on both controllers and both
  hooks now resolves `boolean`: whether the save landed. `useRuleEditor` compares `dirty` too.
- [x] **9. Rules page follows the route.** `App` hands `RulesPage` `selected`/`onSelect` as it does
  the propositions page; the palette navigates, `load` follows the route, and
  `rememberSelection('rules', …)` makes the Rules link round-trip. `RuleHeader` is folded into the
  page; the local-draft palette row is retired. With no rule in the route the page renders the
  `.empty-state` (Choose a rule ⌘K); JSON and Close are unavailable.
- [x] **10. Document actions.** `shell/DocumentActions` is the toolbar both pages render: Open,
  JSON, Close, and `shell/SplitButton` for Save — a filled main button wearing the variant in force,
  a chevron opening a `menu` of `menuitemradio` items (Save / Save & close, each with its
  consequence), arrow-key roving, Escape back to the toggle, `aria-controls` only while mounted.
  Choosing a variant runs it *and* makes it the default, remembered in `localStorage`. Close on a
  dirty document opens `shell/DiscardDialog` (Keep editing / Save & close / Discard changes);
  "& close" waits for `save()` to resolve `true`.
- [x] **11. Verify.** `pnpm typecheck`; unit 460 (studio) + 708 (core) + 29 (react); `pnpm a11y` 54
  (two new hard surfaces: the save menu, the unsaved-changes dialog; the fixture rule is served as a
  code-defined default so `composeRule` has a root row); e2e via `e2e/shell.ts#openScratchRule`,
  since every builder spec used to edit the seed document at `/` and now has to open a rule first; 31 passed, 8 auth skipped.
  `test/setup.ts` shims `localStorage`, which vitest's jsdom leaves undefined.

## Follow-ups

- **Unsaved-changes indicator** — the dirty flag now exists (`EditorState.dirty`); the canvas
  shows where the indicator goes (top bar, before Save).
- **A code-defined default starts from the previous document.** With no local draft, opening a
  rule whose server document is `null` shows whatever the shared store held — the previously
  edited document, or the seed. A fresh starting document for that case is a controller decision.
- **A dirty guard on the page switch and on `beforeunload`** — `dirty` makes both cheap.
- **`.claude/launch.json` `studio-ui`** proxies `/api` to a hard-coded 5100; a
  `MOTIV_STUDIO_API` override would let a worktree develop against its own host.
