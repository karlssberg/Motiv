# Demo promotion inventory

Evidence for ticket 01. Every file under `ui/apps/demo/src` classified. **No boundary is recommended
here** — that is ticket 07's decision. Measured 2026-08-05 against
`ui/apps/demo/src` = 6,819 lines / 54 files.

## Summary totals

| Classification | Files | Lines | % of 6,819 | % of 5,238 (code only, ex-CSS) |
|---|---:|---:|---:|---:|
| **product-generic** | 41 | **4,459** | 65.4% | 85.1% |
| **arguable** | 8 | 584 | 8.6% | 10.3% (584−44 css = 540) |
| **demo-specific** | 5 | 1,776 | 26.0% | 4.6% (1,776−1,537 css = 239) |
| Total | 54 | 6,819 | 100% | |

The 26% demo-specific figure is dominated by one file: `styles/app.css` at 1,537 lines. Strip the two
stylesheets and the picture inverts — **of 5,238 lines of TypeScript, 239 lines (4.6%) are genuinely
demo-specific**: `App.tsx`, `main.tsx`, `CheckoutPane.tsx`, `RulesPage.tsx`.

Model coupling is narrower still. It is **one exported constant**:

```ts
// App.tsx:8
const MODEL_TYPE = 'customer';
```

imported by exactly five files (`BuilderPane`, `CheckoutPane`, `EvaluatePane`, `PropositionsPage`,
`RuleHeader`), plus three inline sample-JSON literals and two hardcoded strings (`'Eligibility rules'`
breadcrumb in `RuleHeader.tsx:143`, `'quota-rule.motiv'` filename in `DslEditor.tsx:26`). There is **no**
occurrence anywhere in `src/` of `Order`, `loyalty-discount`, `can-checkout`, or `fraud-screening` —
those names appear only in `README.md` and the e2e specs. `CheckoutPane` is the only file that knows
what a customer *is*.

### By area

| Area | Files | Lines | generic | arguable | demo-specific |
|---|---:|---:|---:|---:|---:|
| `builder/` | 19 | 1,680 | 1,651 | 29 | 0 |
| `dsl/` | 11 | 1,251 | 1,209 | 42 | 0 |
| `panes/` | 10 | 1,103 | 544 | 396 | 163 |
| `explorer/` | 3 | 607 | 607 | 0 | 0 |
| `shell/` | 5 | 437 | 437 | 0 | 0 |
| `routing/` | 1 | 73 | 0 | 73 | 0 |
| `styles/` | 2 | 1,581 | 0 | 44 | 1,537 |
| root (`App`, `main`, `decorationPatch`) | 3 | 87 | 11 | 0 | 76 |

---

## Per-file table

Legend for **CM**: `value` = runtime import of `@codemirror/*` or `@lezer/*`; `type` = type-only import
(erased at build, no runtime dependency); `via` = pulls CodeMirror transitively through another demo
module; `—` = none.

### `builder/` — 19 files, 1,680 lines

| path | lines | class | from rules-core | from rules-react | CM / 3rd-party | model coupling |
|---|---:|---|---|---|---|---|
| `builder/accordion.ts` | 92 | generic | — | — | — | none — pure tree-view state (collapsed/open/pinned path sets) |
| `builder/childPaths.ts` | 15 | generic | `binaryOperator`, `higherOrderKey`, `isBinaryNode`, `isHigherOrderNode`, `isNotNode`, `operandsOf`, `RuleNode` | — | — | none — pure document traversal; arguably already belongs in core |
| `builder/dslTokens.ts` | 29 | generic | `tokenize`, `TokenKind` | — | **—** | none — CodeMirror-free syntax highlighting over core's lexer |
| `builder/highlight.ts` | 39 | generic | — | — | — | none — hover/selection state machine |
| `builder/mutations.ts` | 73 | generic | `binaryOperator`, `operandsOf`, `BinaryNode`, `BinaryOperator`, `RuleEditorStore`, `RuleNode` | — | — | none — grammar-level, re-declares `BINARY_OPERATORS` / quantifier `KINDS` |
| `builder/nodeSummary.ts` | 65 | generic | 8 predicates + `Countable`, `HigherOrderKey`, `RuleNode` | — | — | none — operator labels and glosses; encodes the n-ary XOR parity rule |
| `builder/DecorationEditor.tsx` | 44 | generic | `RuleNode` | `useRuleEditorStore` | — | none — name/whenTrue/whenFalse fields |
| `builder/ListboxPicker.tsx` | 123 | generic | — | — | — | **zero Motiv coupling** — a generic ARIA listbox-as-inline-text control |
| `builder/NodeDsl.tsx` | 123 | generic | `parse`, `printInline`, `Catalog`, `RuleNode` | `useRuleEditorStore` | via | none |
| `builder/NodeInsertButton.tsx` | 23 | generic | — | — | — | none |
| `builder/NodeMenu.tsx` | 79 | generic | — | `useRuleEditorStore` | — | none |
| `builder/NodeToolbar.tsx` | 29 | **arguable** | `isSpecNode`, `RuleNode` | — | — | none, but the entire body is a *disabled* `expression — coming` placeholder |
| `builder/OperatorPicker.tsx` | 44 | generic | `binaryOperator`, `BinaryNode` | `useRuleEditorStore` | — | none |
| `builder/PendingSlot.tsx` | 75 | generic | `parse`, `Catalog`, `RuleNode` | — | via | none |
| `builder/QuantifierNode.tsx` | 83 | generic | `Catalog`, `HigherOrderNode` | `useRuleEditorStore` | — | none — filters collections by the `modelType` **prop**, not the constant |
| `builder/RuleDslStrip.tsx` | 139 | generic | `parse`, `printInline`, `RuleNode`, `SourceRange` | — | — | none — print/reparse span mapping |
| `builder/RuleNodeEditor.tsx` | 292 | generic | `isBinaryNode`, `isHigherOrderNode`, `firstOperandTarget`, `insertTargetForRow`, `planInsert`, `Catalog` | `useRuleEditorStore`, `useRuleNode` | — | none — takes `modelType` as a prop |
| `builder/useInlineDslEditor.ts` | 197 | generic | `Catalog` | — | **value** — `@codemirror/{autocomplete,commands,state,view}` | none |
| `builder/usePopoverCard.ts` | 116 | generic | — | — | — | none — measure/place/dismiss for row popups |

### `dsl/` — 11 files, 1,251 lines

| path | lines | class | from rules-core | from rules-react | CM / 3rd-party | model coupling |
|---|---:|---|---|---|---|---|
| `dsl/DslEditor.tsx` | 264 | generic | `getNode`, `isSpecNode`, `Catalog`, `NodeSpan`, `RuleDocument`, `RuleEditorStore` | `useRuleEditor` | **value** — `autocomplete`, `commands`, `lint`, `state`, `view` | none (cosmetic: hardcoded `'quota-rule.motiv'` filename) |
| `dsl/PayloadPopover.tsx` | 161 | generic | `getNode`, `Catalog`, `Payload`, `RuleEditorStore` | — | — | none — string-vs-JSON mode driven by `catalog.metadataTypes` |
| `dsl/completion.ts` | 90 | generic | `PARAM_REST_CHARS`, `WORD_REST_CHARS`, `WORD_START_CHARS`, `Catalog` | — | **type only** — `@codemirror/autocomplete` | none — catalog-driven |
| `dsl/hover.ts` | 57 | generic | — | — | **value** (`hoverTooltip` from `@codemirror/view`); type-only for `state`, `lint` | none |
| `dsl/lint.ts` | 64 | generic | `rangeOfPath`, `DslError`, `NodeSpan`, `ParseResult`, `RuleError`, `SourceRange` | — | **type only** — `@codemirror/lint` | none |
| `dsl/motivLanguage.ts` | 109 | generic | `PARAM_REST_CHARS`, `WORD_REST_CHARS`, `WORD_START_CHARS` | — | **value** — `@codemirror/language`, `@lezer/highlight` | none — but see §2, this is a second lexer |
| `dsl/payloadChips.ts` | 108 | generic | — | — | **value** — `@codemirror/{view,state}` | none |
| `dsl/popoverPlacement.ts` | 82 | generic | — | — | **—** (zero imports of any kind) | none — pure viewport arithmetic |
| `dsl/theme.ts` | 42 | **arguable** | — | — | **value** — `@codemirror/view` | none, but binds the editor to the demo's `--dsl-*` / `--mono` custom properties |
| `dsl/useAnchoredCard.ts` | 117 | generic | — | — | **type only** — `@codemirror/view` (uses `coordsAtPos`) | none |
| `dsl/useDslSync.ts` | 157 | generic | `mergeDecorations`, `parse`, `print`, `ParseResult`, `RuleDocument`, `RuleEditorStore` | — | — | none — pure text↔store binding |

### `panes/` — 10 files, 1,103 lines

| path | lines | class | from rules-core | from rules-react | CM / 3rd-party | model coupling |
|---|---:|---|---|---|---|---|
| `panes/AppBar.tsx` | 61 | **arguable** | — | — | — | none to *Customer*, but hardcodes the demo's two-page set and the `M`/`Motiv` wordmark |
| `panes/BuilderPane.tsx` | 86 | **arguable** | `Catalog`, `RulesApiClient` | `useCatalog`, `useRuleEditor`, `useRuleEditorStore` | — | **imports `MODEL_TYPE`** — one line (`<RuleNodeEditor modelType={MODEL_TYPE}/>`); otherwise generic |
| `panes/CheckoutPane.tsx` | 125 | **demo-specific** | `validateAgainstSchema`, `EvaluationResult`, `JsonSchema`, `RulesApiClient`, `SchemaViolation` | — | — | **total** — `SAMPLE_CUSTOMER` literal, `CheckoutResponse{approved,eligibility,screening}`, raw `POST /api/checkout`, names `CanCheckoutRule`/`FraudScreeningRule` in its doc comment |
| `panes/DocumentModal.tsx` | 34 | generic | — | `useRuleEditor`, `useRuleEditorStore` | — | none |
| `panes/EditorPane.tsx` | 83 | generic | `RulesApiClient` | `useCatalog`, `useRuleEditorStore` | via `DslEditor` | none (one disabled `parameters — coming` button) |
| `panes/EvaluatePane.tsx` | 75 | **arguable** | `validateAgainstSchema`, `RulesApiClient`, `SchemaViolation` | `JustificationTree`, `useCatalog`, `useEvaluation`, `useRuleEditor`, `useRuleEditorStore` | — | **`MODEL_TYPE` + `SAMPLE_MODEL`** = `{age,isActive,orderCount}`, i.e. a Customer |
| `panes/PropositionsPage.tsx` | 400 | generic | `DependentEntry`, `PropositionListEntry`, `PropositionSaveResult`, `RulesApiClient` | `useRuleEditor`, `useRuleEditorStore` | — | **one line** — `MODEL_TYPE` as the fallback when the listing is empty (`:183`). Everything else is governance workflow |
| `panes/RuleHeader.tsx` | 174 | **arguable** | `RuleListEntry`, `RulesApiClient` | `useRuleEditor`, `useRuleEditorStore` | — | `MODEL_TYPE` rendered as a pill; hardcoded `'Eligibility rules'` breadcrumb. Versioned save + conflict logic is generic |
| `panes/RulesPage.tsx` | 38 | **demo-specific** | `RuleListEntry`, `RulesApiClient` | — | — | composes `CheckoutPane`; its own comment calls the duplicate-catalog-fetch "a deliberate seam this demo exists to show" |
| `panes/SchemaViolations.tsx` | 27 | generic | `SchemaViolation` | — | — | none |

### `explorer/` — 3 files, 607 lines

| path | lines | class | from rules-core | from rules-react | CM / 3rd-party | model coupling |
|---|---:|---|---|---|---|---|
| `explorer/DependentsStrip.tsx` | 32 | generic | `DependentEntry` | — | — | none — blast-radius summary |
| `explorer/PropositionDialog.tsx` | 215 | generic | `PropositionListEntry` | — | — | none (one placeholder string `customer.eligibility.is-eligible`) — New/Derive/Override as one seeded create |
| `explorer/PropositionExplorer.tsx` | 360 | generic | `buildNamespaceTree`, `countLeaves`, `filterTree`, `NamespaceNode`, `PropositionListEntry` | — | — | none — origin (Compiled/Overridden/Authored) and quarantine badges, namespace tree |

### `shell/` — 5 files, 437 lines

| path | lines | class | from rules-core | from rules-react | CM / 3rd-party | model coupling |
|---|---:|---|---|---|---|---|
| `shell/CommandPalette.tsx` | 141 | generic | — | — | — | **zero Motiv coupling** — generic search-first chooser, `<T extends PaletteItem>` |
| `shell/Modal.tsx` | 90 | generic | — | — | — | **zero Motiv coupling** — native `<dialog>` + `showModal()` |
| `shell/Toolbar.tsx` | 57 | generic | — | — | — | **zero Motiv coupling** — `unavailable` carries the reason, not a boolean |
| `shell/icons.tsx` | 95 | generic | — | — | — | none — hand-drawn inline SVG, deliberately no icon package |
| `shell/useCommandKey.ts` | 54 | generic | — | — | — | **zero Motiv coupling** — ⌘K, inert while any `dialog[open]` |

### Root, routing, styles — 6 files, 1,741 lines

| path | lines | class | from rules-core | from rules-react | CM / 3rd-party | model coupling |
|---|---:|---|---|---|---|---|
| `App.tsx` | 65 | **demo-specific** | `RuleEditorStore`, `RulesApiClient`, `createValidationController` | `RuleEditorProvider` | — | **defines `MODEL_TYPE = 'customer'`**; seeds the store with `{spec: 'customer.is-active'}` |
| `main.tsx` | 11 | **demo-specific** | — | — | `react-dom/client` | mounts the demo, imports the two stylesheets |
| `decorationPatch.ts` | 11 | generic | `Decoration` | — | — | none — an `exactOptionalPropertyTypes` escape hatch that arguably belongs in core |
| `routing/useHashRoute.ts` | 73 | **arguable** | — | — | — | none to Customer; the `Page = 'rules' \| 'propositions'` union is the demo's page set. Parse/format/listen is generic |
| `styles/app.css` | 1,537 | **demo-specific** | — | — | — | the demo's whole visual identity; two "load-bearing" flexbox comments; class names are global and un-namespaced |
| `styles/tokens.css` | 44 | **arguable** | — | — | — | design tokens (`--dsl-*` etc.), traceable to a source design `MotivShell.dc.html` |

---

## 1. Heavy-dependency promotions — the CodeMirror surface

`@codemirror/*` is **six packages** plus `@lezer/highlight` in `ui/apps/demo/package.json`.
`@motiv/rules-core` has **zero runtime dependencies** (`ajv` is a devDependency, for tests);
`@motiv/rules-react` has one (`@motiv/rules-core`) plus `react` as a peer. Promoting anything below
would be the first heavy dependency either package has ever taken.

**Files that import CodeMirror at runtime (`value` imports — a real dependency):**

| file | lines | packages |
|---|---:|---|
| `dsl/DslEditor.tsx` | 264 | `autocomplete`, `commands`, `lint`, `state`, `view` |
| `builder/useInlineDslEditor.ts` | 197 | `autocomplete`, `commands`, `state`, `view` |
| `dsl/motivLanguage.ts` | 109 | `language`, **`@lezer/highlight`** |
| `dsl/payloadChips.ts` | 108 | `view`, `state` |
| `dsl/hover.ts` | 57 | `view` (`hoverTooltip`) |
| `dsl/theme.ts` | 42 | `view` |
| **direct total** | **777** | |
| `builder/NodeDsl.tsx` (via `useInlineDslEditor`) | 123 | |
| `builder/PendingSlot.tsx` (via `useInlineDslEditor`) | 75 | |
| `panes/EditorPane.tsx` (via `DslEditor`) | 83 | |
| **with transitive hosts** | **1,058** | |

**Files that touch CodeMirror only through the type system (`import type` — erased at compile,
no runtime dependency; `@codemirror/*` needed only as a devDependency for `.d.ts`):**

| file | lines | what it imports |
|---|---:|---|
| `dsl/completion.ts` | 90 | `type { Completion, CompletionContext, CompletionResult }` |
| `dsl/lint.ts` | 64 | `type { Diagnostic }` |
| `dsl/useAnchoredCard.ts` | 117 | `type { EditorView }` — but calls `view.coordsAtPos`, so useless without one |
| **total** | **271** | |

**Files with no CodeMirror relationship at all, that nonetheless do DSL work:**

- `builder/dslTokens.ts` (29) — **checked, does not import CodeMirror.** It builds highlighted token
  runs from core's `tokenize`, re-inserting the whitespace gaps the lexer skips. This is the
  existence proof that a CodeMirror-free highlighter over the core lexer is already viable — the
  demo ships one, and uses it for every collapsed builder row.
- `builder/highlight.ts` (39), `dsl/popoverPlacement.ts` (82) — pure, zero imports.

**The answer to the question the ticket poses.** A package *could* export completion sources and lint
diagnostics without importing CodeMirror: `completion.ts` and `lint.ts` (154 lines) already only
depend on CM's *shapes* — `Completion`, `CompletionContext`, `CompletionResult`, `Diagnostic` — which
are structurally satisfiable by locally-declared interfaces, at the cost of losing compile-time
agreement with the real CM types. The **highlight grammar cannot**: `motivLanguage.ts` is a
`StreamParser` plus a `HighlightStyle` over `@lezer/highlight` tags, and both are CodeMirror
constructs by definition. The nearest CM-free equivalent is `dslTokens.ts`'s flat token-run model,
which is what a package would have to export instead (and which an app can trivially adapt into a
`StreamParser`).

So the option space is: **(a)** promote analysis only — completion + lint + a token-run
highlighter, ~250 lines, zero new dependencies, editor-agnostic; **(b)** promote the editor — add
`motivLanguage`, `theme`, `payloadChips`, `hover`, `DslEditor`, `useInlineDslEditor`, and six
`@codemirror/*` peer dependencies for ~1,058 lines.

---

## 2. Duplication against the packages — is `demo/src/dsl/` a reimplementation?

`ui/apps/demo/src/dsl/` = 1,251 lines (11 files). `ui/packages/rules-core/src/dsl/` = 780 lines
(6 files: `types` 51, `lexer` 112, `parser` 366, `printer` 182, `decorations` 31, `spans` 32).

**Verdict: overwhelmingly editor integration, with one lexer-shaped hole.** ~1,052 of the 1,251 lines
never parse anything; ~199 lines partially reimplement the core lexer's classification and vocabulary.

**Integration, not duplication (~1,052 lines).** `useDslSync.ts` (157) is a two-way binding that calls
core's `parse`, `print` and `mergeDecorations` and holds no grammar knowledge. `lint.ts` (64) maps
core's `DslError` / `RuleError` through core's `rangeOfPath` into CM `Diagnostic`s. `DslEditor.tsx`
(264) pushes core-derived spans and diagnostics into the view. `payloadChips`, `useAnchoredCard`,
`popoverPlacement`, `PayloadPopover`, `theme` (510) do no language work whatsoever.

**Genuine reimplementation (~199 lines).**

1. **`dsl/motivLanguage.ts:52–89` is a second lexer.** Its `token(stream)` re-derives the same
   classification `rules-core/src/dsl/lexer.ts` `tokenize()` performs — two-char `&&`/`||`, single
   `&|^!`, parens vs braces, `:`/`=`, `"`-string and backtick-expression delimited runs, `@`-param,
   and the two numeric edge rules — as an incremental `StreamParser`. The two files carry
   near-identical *prose* for both edge rules:

   - core lexer: *"A `-` starts a number only when a digit follows it. Elsewhere `-` is either part
     of a spec word (`is-active`, consumed whole below) or an unrecognised character."*
   - `motivLanguage.ts`: *"A `-` starts a number only when a digit follows; elsewhere it is part of
     a spec word (consumed whole below) or an unrecognised character."*

   Only the three character classes are actually shared, via core's exported `WORD_START_CHARS`,
   `WORD_REST_CHARS`, `PARAM_REST_CHARS`.

2. **The DSL vocabulary exists in three places.** `motivLanguage.ts:11–17` declares
   `DSL_KEYWORDS = ['param','in','as']`, `DSL_QUANTIFIERS = ['all','any','exactly','atLeast','atMost']`,
   `DSL_TYPES = ['integer','number','string','boolean']`. These are byte-identical to
   `rules-core/src/dsl/lexer.ts:3–5`'s `KEYWORDS` / `TYPES` / `QUANTIFIERS`, which are **module-private
   and not exported**. `completion.ts` then imports the demo's copies to build its option list.
   `builder/mutations.ts` adds a fourth copy of adjacent grammar knowledge (`BINARY_OPERATORS`,
   quantifier `KINDS`).

3. **The codebase already documents that this duplication has bitten.** `rules-core/src/dsl/lexer.ts`
   lines 8–14, verbatim: the character classes are exported *"so that anything outside this module
   needing to recognise or complete a word — the demo's CodeMirror stream parser and its completion
   source, currently — composes its own regex from these instead of hand-copying the character
   classes. That copying is exactly how they drifted out of sync with this lexer when dots were
   admitted below: both `WORD_START`/`WORD_REST` and their demo duplicates needed the same edit, and
   only one side got it."* The same incident is re-narrated in `motivLanguage.ts:22–27` and
   `completion.ts:5–11`. The fix applied was to share the *character classes*; the classification
   logic and the vocabulary were left duplicated.

**Also duplicated, outside `dsl/`:** `builder/usePopoverCard.ts` (116) and `dsl/useAnchoredCard.ts`
(117) are two hooks over the same `placePopover`, with an identical private `samePlacement` helper
and the same measure-in-layout-effect / listen-on-scroll-capture / hidden-until-placed structure.
They differ only in what they anchor to (a DOM trigger vs a CodeMirror document position).

---

## 3. Load-bearing seam comments

`ui/apps/demo/README.md` closes with: *"The builder is the accordion under
`ui/apps/demo/src/builder/`; load-bearing seams are marked with code comments."*

**The claim is wrong as written. There is not a single `Seam:` comment anywhere in `builder/`.** All
of them are one directory up:

| location | marks |
|---|---|
| `App.tsx:16` | **the transport.** `RulesApiClient` is the only thing that talks to the backend (`GET /catalog`, `POST /validate`, `POST /evaluate`); swap `baseUrl` or inject a custom `fetch` |
| `App.tsx:24` | **live validation.** `createValidationController` debounces store edits to `/validate` and writes errors back onto the store; flips to the async path when the loaded rule is async |
| `App.tsx:38` | **the store hookup.** `RuleEditorProvider` exposes the single `RuleEditorStore` to every builder component below it |
| `panes/RuleHeader.tsx:44` | **dynamic replacement.** Picks a live server rule, loads it into the shared store, saves it back with the loaded version; a stale version is a conflict banner |
| `panes/CheckoutPane.tsx:21` | **the rule being *used*.** `POST /api/checkout` runs the live `CanCheckoutRule` (sync) and `FraudScreeningRule` (async); the pane deliberately talks raw HTTP with no `RulesApiClient` "to show the consuming side" |
| `panes/RulesPage.tsx:28` | *(unprefixed)* each pane's self-contained wiring — four catalog fetches for one static payload — is "a deliberate seam this demo exists to show", so the duplication is accepted |

The two `load-bearing` comments in the tree are both in `styles/app.css` (lines 591 and 1089) and are
about flexbox `min-width: 0` and `flex: 0 0 auto` — CSS layout, not architecture.

What `builder/` *does* carry instead is unusually dense design-rationale prose — the press/release
guard in `RuleNodeEditor.releaseMatchesPress`, the `attached` re-entrancy guard in
`useInlineDslEditor`, `NodeInsertButton`'s justification for one insert rule, `PendingSlot`'s
argument for keeping the uncommitted node out of the document. Valuable, but rationale rather than
seam markers.

**Two further README staleness notes:** it documents a pane at `src/panes/JsonPane.tsx` which no
longer exists (it is now `DocumentModal.tsx`, a modal rather than a column), and it describes a
three-column shell that the routing rework replaced.

---

## 4. The 218-line question

**Empirically: small because nothing pushed on it, not because the boundary is right.**

Four pieces of evidence.

**(a) The volume is there and it is model-agnostic.** 4,459 lines / 41 files are product-generic and
already model-agnostic *as written* — 85% of the non-CSS code. Not "could be made agnostic": they
name no model type, take `modelType` as a prop where they need one, and drive every choice off the
catalog (`catalog.specs`, `catalog.collections`, `catalog.metadataTypes`, `catalog.modelTypes`).
Whole directories have **zero** coupling of any kind: `shell/` (437 lines, and zero imports from
either Motiv package), `explorer/` (607).

**(b) The coupling that does exist is a module constant, not a design.** Five files import
`MODEL_TYPE` from `App.tsx`. Four of the five uses are a single expression each
(`modelType={MODEL_TYPE}`, `catalog.modelTypes?.[MODEL_TYPE]`, `…sort()[0] ?? MODEL_TYPE`, a
breadcrumb pill). The rework to promote them is to accept the value as a prop — mechanical, and
`RuleNodeEditor` / `QuantifierNode` / `PendingSlot` already do exactly that internally. Only
`CheckoutPane` (125) is irreducibly demo-specific; it is the one file that knows a customer has an
age and orders and that `/api/checkout` exists.

**(c) The package's one non-trivial component was rejected by its only consumer.**
`@motiv/rules-react` exports seven things. Six are used by the demo. The seventh — `RuleTree`
(42 lines, a headless pre-order flatten with ARIA `treeitem` wrappers and a render prop) — is
**imported by nothing in `ui/apps/demo/src`**. The demo instead wrote `builder/RuleNodeEditor.tsx`,
292 lines of its own recursion, because it needed per-node collapse, pinning, popover arbitration,
insertion slots, and a caret whose meaning depends on whether the node has children. That is the
single sharpest datum available: the package's attempt at an authoring component exists, and the app
that consumes the package did not use it.

**(d) What is genuinely *not* promotable as written is not model coupling.** Two things:

1. **Styling.** Not one component in `ui/apps/demo/src` ships styles. Every one emits unscoped class
   names — `.node-row`, `.palette-list`, `.dsl-popover`, `.toolbar-slot`, `.model-pill` — into a
   1,537-line global stylesheet, and several depend on it behaviourally, not just visually
   (`usePopoverCard` and `useAnchoredCard` measure real boxes; `payloadChips` renders an empty button
   whose glyph comes from CSS; `PropositionExplorer` styles on `aria-pressed`). Promoting a
   component means either shipping CSS from a package, adopting a class-name convention, or
   inverting to headless render props — a decision with no precedent in this repo.
2. **CodeMirror**, per §1 — 1,058 lines behind six packages.

**The rework tally, if the goal is promotability:**

| bucket | lines | what promotion costs |
|---|---:|---|
| reusable as written, no new deps, no model rework | ~3,400 | class-name/styling story only |
| reusable after threading `modelType` as a prop | ~800 | 5 mechanical edits + styling story |
| reusable but drags `@codemirror/*` | ~1,058 | six peer dependencies |
| not promotable | ~239 (+1,581 CSS) | `App`, `main`, `CheckoutPane`, `RulesPage`, stylesheets |

---

## Hardest calls

**`dsl/motivLanguage.ts` (109) — generic by function, duplicate by construction.** By every column in
the table it is product-generic: no model coupling, driven by the same character classes the core
lexer exports. But it is a *second implementation* of core's `tokenize`, and the repo has already
recorded one drift incident between the two. The classification hinges on a question the table cannot
answer: does the SDK own a CodeMirror grammar, or does it export a token stream (as `builder/dslTokens.ts`
already proves it can) and leave grammar construction to the app? Under the first reading the file is
generic and the duplication is a bug to be fixed by promoting it. Under the second it is app-side
editor plumbing forever and the duplication is a *cost of the boundary*, to be reduced only by having
core export its vocabulary sets. Filed as product-generic because nothing in the file is about this
demo — but noted as the file most likely to be reclassified by ticket 07.

**`panes/RuleHeader.tsx` (174) + `panes/PropositionsPage.tsx` (400) — the flagship app, in 574 lines.**
Versioned optimistic save, conflict detection with a reload escape hatch, blast-radius reporting
before the save rather than after, revert-vs-delete disambiguation read off the entry because the
`DELETE` response cannot tell them apart, continuation guards so an in-flight answer never lands on a
selection the user has moved off. None of this is *SDK*, and none of it is *demo throwaway* — it is
precisely the governance app the destination describes. The classification hinges on whether ticket 07
treats "the app" as a promotion target at all, or only the packages. Filed `PropositionsPage` as
generic (its coupling is one fallback expression) and `RuleHeader` as arguable (a `MODEL_TYPE` pill and
a hardcoded `'Eligibility rules'` breadcrumb) — but the honest reading is that both are the same kind
of thing, and the split between them is an artefact of two cosmetic strings.

**`shell/` (437, five files, zero Motiv imports) — too generic to classify usefully.** `Modal`,
`CommandPalette`, `Toolbar`, `useCommandKey` and `icons` know nothing about rules, propositions,
documents or the API. Every one is product-generic on the table's own terms. But that is exactly why
the call is hard: promoting them means `@motiv/rules-react` starts shipping a general-purpose UI kit —
a `<dialog>` wrapper, a ⌘K palette, an SVG icon set — which is a different product commitment from
"headless React adapter for `@motiv/rules-core`" (its own package description). The evidence says
reusable; whether *this* SDK should be the thing that reuses it is a boundary question.

**`routing/useHashRoute.ts` (73) — generic mechanism, demo-specific alphabet.** `parseHash`,
`formatHash`, the `hashchange` listener, the `decodeURIComponent` guard against a `URIError` thrown
during render — all generic and well-argued. The `Page = 'rules' | 'propositions'` union is the demo's
own page set, and the file's comment even explains that hash routing was chosen so a *fork* needs no
server-side fallback, which reads as an invitation to copy rather than to import. Arguable, hinging on
whether the type is parameterised or the file is treated as a scaffold.
