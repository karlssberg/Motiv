# Motiv DSL Editor — Design

**Date:** 2026-07-25
**Status:** Approved (design)
**Source:** Faithful implementation of the Claude Design prototype `Motiv DSL Editor.dc.html`
(components `DslEditor` + `MotivShell`), imported from
`https://claude.ai/design/p/4c788b61-b45b-4e0b-b47c-a1d6e6453b46`.

## Summary

Add a textual `.motiv` DSL editor as a fourth surface in the demo shell, toggled
against the Builder on the left while the JSON and Evaluate panes stay live. The DSL
is the source of truth: typing debounce-parses to a `RuleDocument` and commits it to
the shared `RuleEditorStore`, so Builder / JSON / Evaluate update from the text.

The editor is built on **CodeMirror 6** and provides Motiv syntax highlighting,
catalog-driven autocomplete, error squiggles with hover tooltips (both syntactic and
semantic), a per-spec **payload popover** (string ⇄ object mode), a **Format** button
(canonical reprint), a **sync pill**, and a **conflict banner** for when the Builder
changes the rule while the DSL buffer is dirty.

## Decisions (locked)

1. **Full faithful port** — everything the `DslEditor` prototype shows, production quality.
2. **CodeMirror 6** for the editor surface (not a hand-rolled `textarea`+overlay).
3. **Text is the source of truth** with a debounced commit into `RuleEditorStore`.
4. **Clean text; payloads merged from the store** — the DSL text encodes structure and
   node names (`as "…"`) only. `whenTrue` / `whenFalse` payloads live on the store node,
   are edited via the popover, and are re-attached to freshly parsed documents by path
   alignment. This matches the prototype visually.

## Scope

### In scope

- A real `.motiv` DSL with a two-way parser/printer over the **entire** `rule.v1.json`
  grammar (spec, expression, not, and/or/xor/andAlso/orElse, all five higher-order
  quantifiers, parameters, `@param` references, node names).
- CodeMirror 6 editor: Motiv syntax highlighting, catalog-driven autocomplete, lint
  squiggles + hover tooltips, payload popover, Format button, sync pill, conflict banner,
  and the "Rule returns" header strip.
- Integration into the demo as a **Builder ⇄ DSL** toggle; JSON + Evaluate unchanged.

### Non-goals

- **Slate/Warm design-system switcher** and the light/dark toggle chrome from `MotivShell`.
  That is design-tool furniture around the editor, not the editor. The demo already themes
  via `prefers-color-scheme`; the editor will be theme-aware but no switcher is added.
- **DSL comments** — deferred. Grammar leaves room to add `//` later.
- **"Rule returns" as a backend concept.** The backend derives return shape from metadata
  types, not a rule-level toggle. The strip is rendered as a *display* of the rule's return
  shape (from the catalog/metadata), not a new interactive backend switch.

## Architecture

Three layers along the existing monorepo seams.

### `rules-core/src/dsl/` — pure language layer (zero dependencies)

The TDD heart. No React, no CodeMirror.

- `lexer.ts` — `tokenize(text): Token[]`, each token carrying `{ kind, value, from, to }`
  source offsets.
- `parser.ts` — `parse(text): ParseResult` where

  ```ts
  interface NodeSpan { path: string; from: number; to: number }
  interface DslError { from: number; to: number; code: string; message: string }
  interface ParseResult {
    document?: RuleDocument;   // present when there are no fatal syntax errors
    errors: DslError[];
    spans: NodeSpan[];         // backend path (e.g. "$.rule.andAlso[2]") → text range
  }
  ```

  `spans` is what lets backend `RuleError`s (which are keyed by backend path) map back to
  text ranges, and what lets a click on a spec token resolve the node's path.
- `printer.ts` — `print(document): string`. Canonical pretty-print; this is exactly what
  the **Format** button produces. Minimal parenthesisation: a child is wrapped only when
  its operator precedence is looser than its parent's.
- `decorations.ts` — `mergeDecorations(parsed: RuleDocument, prior: RuleDocument):
  RuleDocument`. Re-attaches `whenTrue` / `whenFalse` from `prior` onto `parsed` by walking
  both with `listPaths` and copying decorations where the path exists in both **and** the
  node kind matches. Decorations whose path no longer exists (structural edit) are dropped.
  Names are carried by the text itself, so they are not merged.

### `demo/src/dsl/` — CodeMirror 6 integration + React

- `motivLanguage.ts` — a `StreamLanguage` tokenizer mapping Motiv tokens to highlight tags
  (spec, operator, keyword, quantifier, string, number, param-ref, collection path, brace).
- `completion.ts` — an `autocompletion` source built from the live catalog and the
  document's own `param` declarations: specs, collections, quantifiers, keywords
  (`as`, `in`, `param`, type names), and `@param` refs. Typed prefix is bolded.
- `lint.ts` — a `linter` source returning the union of parser `DslError`s (offsets native)
  and `/validate` `RuleError`s mapped through `spans` (path → range).
- `hover.ts` — `hoverTooltip` rendering the dark tooltip (kind · code · message · path).
- `theme.ts` — an `EditorView.theme` bound to the demo's CSS custom properties so the
  editor tracks light/dark.
- `PayloadPopover.tsx` — the spec payload editor (name + whenTrue/whenFalse; string mode
  for `Explanation` metadata, object/JSON mode with schema-driven completion otherwise).
- `useDslSync.ts` — the text ↔ store bridge and sync/conflict state machine (below).
- `DslEditor.tsx` — assembles the CodeMirror instance, the "Rule returns" strip, the sync
  pill, the conflict banner, and the popover.

CodeMirror dependencies (`@codemirror/state`, `@codemirror/view`, `@codemirror/language`,
`@codemirror/autocomplete`, `@codemirror/lint`, `@codemirror/commands`, `@lezer/highlight`)
are added to the **demo** package only. `rules-core` and `rules-react` stay
dependency-light. The pure language layer is unit-testable in isolation; only the demo
pulls the editor weight. The `demo/src/dsl/` modules could later graduate into a package.

### Demo shell integration

A **Builder ⇄ DSL** segmented toggle in the left pane region selects which editor is
shown; JSON and Evaluate panes are untouched. Both surfaces read and write the same
`RuleEditorStore` provided by `RuleEditorProvider`.

## The DSL grammar

Precedence follows C# (which Motiv mirrors), tightest → loosest:
`!` › `&` › `^` › `|` › `&&` › `||`. Consecutive same-operator operands flatten into a
single n-ary node (Motiv's `and`/`or`/… arrays require `minItems: 2`).

The precedence-climbing chain is ordered loosest (outermost) → tightest (innermost), so
that `||` is parsed at the top and `&` binds most tightly of the binary operators — exactly
the C# ordering above.

```
document   := param*  expr
param      := 'param' IDENT ':' ('integer'|'number'|'string'|'boolean') ('=' literal)?
expr       := orElse
orElse     := andAlso ('||' andAlso)*   # || → orElse   (loosest)
andAlso    := or      ('&&' or)*        # && → andAlso
or         := xor     ('|'  xor)*       # |  → or
xor        := and     ('^'  and)*       # ^  → xor
and        := unary   ('&'  unary)*     # &  → and       (tightest binary)
unary      := '!' unary | postfix       # !  → not
postfix    := primary ('as' STRING)?    # as "x" → node.name = "x"
primary    := SPEC                      # is-active → { spec }
            | '`' EXPR '`'              # `n > 0`   → { expression }
            | '(' expr ')'
            | quantifier
quantifier := ('all'|'any') 'in' PATH '{' expr '}'
            | ('exactly'|'atLeast'|'atMost') '(' N ')' 'in' PATH '{' expr '}'
N          := INT | '@' IDENT           # countable: literal or @paramRef
```

Consequence worth noting: because the single-char logical operators bind tighter than the
conditional ones, `a & b && c` parses as `(a & b) && c` and `a | b || c` as `(a | b) || c`
— matching C#. Mixed-operator expressions are rare; the printer parenthesises defensively
(see below) so round-tripped text never relies on the reader knowing this table.

Mapping to `rule.v1.json` node kinds:

| DSL | Node |
| --- | --- |
| `is-active` | `{ spec: "is-active" }` |
| `` `n > 0` `` | `{ expression: "n > 0" }` |
| `!x` | `{ not: x }` |
| `a & b` | `{ and: [a, b] }` |
| `a && b` | `{ andAlso: [a, b] }` |
| `a ^ b` | `{ xor: [a, b] }` |
| `a \| b` | `{ or: [a, b] }` |
| `a \|\| b` | `{ orElse: [a, b] }` |
| `all in orders { … }` | `{ asAllSatisfied: …, path: "orders" }` |
| `any in orders { … }` | `{ asAnySatisfied: …, path: "orders" }` |
| `exactly(2) in orders { … }` | `{ asNSatisfied: …, n: 2, path: "orders" }` |
| `atLeast(@minOrders) in orders { … }` | `{ asAtLeastNSatisfied: …, n: "@minOrders", path: "orders" }` |
| `atMost(3) in orders { … }` | `{ asAtMostNSatisfied: …, n: 3, path: "orders" }` |
| `x as "quota"` | `x` with `name: "quota"` |
| `param minOrders: integer = 3` | `parameters.minOrders = { type: "integer", default: 3 }` |

**`as` binds to the primary on its left.** In
`atLeast(@minOrders) in orders { … } as "quota"` the name lands on the *quantifier* node —
exactly what `MotivShell`'s reference JSON shows (the `asAtLeastNSatisfied` element is named
`"quota"`, not the enclosing `andAlso`). Naming a compound requires grouping:
`(a && b) as "x"`.

Expression nodes use backtick delimiters. The prototype did not exercise them, but the
grammar includes them so the round-trip is lossless over the whole schema.

### Canonical formatting (Format button)

`print` reproduces the prototype's default layout: parameters first (one per line), a blank
line, then the expression with quantifier bodies indented inside `{ … }` and grouped
sub-expressions wrapped across lines when they contain a quantifier or exceed width. The
prototype's `DEFAULT_TEXT` is the reference target:

```
param minOrders: integer = 3

is-active && (
    is-verified | !is-flagged
) && atLeast(@minOrders) in orders {
    is-positive && is-recent
} as "quota"
```

## Sync state machine (text = source of truth)

`useDslSync` owns `editorText`, `baseDoc` (the document the current text is known-equal to),
and `status ∈ { synced, dirty, error }`.

- **Text edit** → debounce 300ms → `parse(text)`:
  - **Clean parse** → `merged = mergeDecorations(parsed, store.document)`. Set a
    `selfCommitting` flag, call `store.loadDocument(merged)`, then set
    `baseDoc = store.getState().document` (the store clones on load, so `baseDoc` must be
    the stored clone, not the pre-clone `merged`); `status = synced`.
  - **Parse errors** → do **not** commit; `status = error`; lint surfaces squiggles.
- **Store change we did not cause** (Builder edited the tree — any store notification whose
  `document !== baseDoc` that did not originate from our own commit):
  - buffer clean & `synced` → silently reprint `print(store.document)` into the editor and
    update `baseDoc`.
  - buffer **dirty / unparseable** → **cancel any pending commit**, then show the
    **conflict banner** (*"The rule changed in the Builder while your DSL was unsaved."*):
    - **Reformat from tree** → `print(store.document)` into the editor, discard local text,
      `status = synced`.
    - **Keep editing** → dismiss banner and re-arm the debounced commit; local text is the
      chosen source and commits normally.

  Cancelling on entry to the conflict state is load-bearing. A pending commit is a decision
  already in flight: if the debounce fired while the banner was up, the buffer would
  overwrite the Builder's change and **Reformat from tree** would merely reprint the user's
  own text — offering a choice that no longer exists. The two versions must stay held apart
  until the user picks one.

A guard prevents our own `loadDocument` commits from being seen as "external": the store
subscription handler, when the `selfCommitting` flag is set, records `baseDoc` from the
current store document, clears the flag, and returns without running conflict handling.
Notifications that are not document changes (`document === baseDoc`, e.g. `setErrors` from
the validation controller) are also ignored — mirroring how `createValidationController`
already filters non-document notifications.

The **sync pill** renders `status`: `synced` (green) · `dirty` (amber) · `error` (red).

## Payload popover

Clicking a spec token resolves the node's backend **path** via `spans`, then opens the
popover for that node:

- **Name** field → `store.setName(path, …)`.
- **When true / When false**:
  - `Explanation` metadata → two string fields → `store.setDecoration(path, { whenTrue,
    whenFalse })` as strings.
  - object metadata → JSON editors with completion driven by the catalog's `metadataTypes`
    schema for the node; `schema.ts::validateAgainstSchema` backs inline validation. Saved
    as objects; the schema requires a `name` when payloads are objects (per `rule.v1.json`
    `sameKindPayloads`).
- **Save** commits to the store. Because payloads merge from the store (not the text), the
  edit survives subsequent text edits and shows immediately in JSON/Evaluate.

## Autocomplete & lint detail

- **Autocomplete** (`@codemirror/autocomplete`): completion source keyed on the token under
  the caret. Sources: catalog specs (with description), catalog collections
  (`path · Element[]`), quantifiers (`all`, `any`, `exactly(n)`, `atLeast(n)`, `atMost(n)`),
  keywords (`as`, `in`, `param`, type names), and `@param` refs from the document's own
  declarations. The typed prefix is bolded in the option label.
- **Lint** (`@codemirror/lint`): diagnostics are the union of
  - parser `DslError`s (native offsets), and
  - `/validate` `RuleError`s mapped `path → { from, to }` via `spans`
  (e.g. `UnknownSpec` becomes a squiggle on the offending spec token).
  The pane is marked out-of-sync while any diagnostic is present, per the prototype.
- **Hover** (`hoverTooltip`): the dark tooltip shows kind · code · message · path.

## Testing (TDD)

Strict red-green-refactor; every unit written test-first.

### `rules-core/src/dsl` (Vitest, pure)

- Lexer: token kinds and exact offsets.
- Parser: each node kind; precedence and n-ary flattening; parameters; all five
  quantifiers; `as`-binds-to-primary; backtick expressions; `@param` countables.
- Parser errors: unterminated group, missing `{`, unknown token, stray `)` — asserted with
  exact `{ from, to, code }`.
- Printer: canonical layout equals the reference `DEFAULT_TEXT`.
- Round-trip: `parse(print(doc)).document` structurally equals `doc`; `print(parse(text))`
  equals the canonical form of `text`.
- `mergeDecorations`: payload re-attached on identical structure; dropped when the path no
  longer exists or the node kind changes.

### `demo` (Vitest + Testing Library)

- DslEditor renders highlighted tokens.
- Autocomplete opens, filters by prefix, inserts.
- Lint squiggle appears for an unknown spec.
- Popover: open → edit → save mutates the store (assert via store state / JSON pane).
- Sync pill transitions synced → dirty → error.
- Conflict banner appears on a Builder edit while the DSL is dirty; both actions behave.

### `demo` (Playwright e2e)

- Type in the DSL pane → JSON and Evaluate panes update.
- Edit in the Builder while the DSL buffer is dirty → conflict banner; Reformat and Keep
  editing both work.

### Verification

Run the full `ui` test suite (`pnpm -C ui test`), typecheck, build, then a
`code-simplifier` pass over the changed files (per project convention) before completion.

## Risks & mitigations

- **Decoration/structure alignment fragility** (accepted with decision 4): payloads keyed by
  path can mis-attach or drop under structural edits. Mitigation: match on path **and** node
  kind; drop rather than mis-assign on mismatch; cover in `mergeDecorations` tests.
- **CodeMirror in jsdom**: some view features need layout. Mitigation: unit-test the pure
  language layer exhaustively; keep component tests behavioural; put geometry-dependent flows
  in Playwright.
- **Grammar completeness vs. the prototype's regex fake**: the prototype tokenizer is not a
  real parser. The grammar above is authored against `rule.v1.json`, not the prototype's
  tokenizer, so it covers node kinds the prototype omitted (expressions, `exactly`/`atMost`,
  `all`/`any`).
