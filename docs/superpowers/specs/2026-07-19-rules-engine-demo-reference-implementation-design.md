# Rules-Engine Demo → Reference Implementation — Design

**Date:** 2026-07-19
**Status:** Approved (pending spec review)
**Scope:** `ui/apps/demo` and `src/examples/Motiv.RulesEngine.Sample` only.

## Problem

The rules-engine demo shipped as a *proof*: four small React files with inline
styles only, a builder that covers just `AND` / `OR` / add-operand / remove, and
no styling worth reading. A developer who clones it can see the packages wired
together, but there is nothing they can comfortably **run with** — neither a
clean run-and-learn experience nor a fork-and-extend skeleton.

The goal is to elevate the demo into a **reference implementation**: something a
developer clones, runs in one command, and forks as the starting skeleton of
their own rules UI. It must work equally well as a *learn-fast demo* and a
*fork-and-extend skeleton*.

## Guiding Finding

The rule-document model already carries the **full grammar**. Every node type in
`ui/packages/rules-core/src/document.ts` extends `Decoration`
(`name` / `whenTrue` / `whenFalse`), and the model already includes `NotNode`,
binary operators (`and` / `or` / `xor` / `andAlso` / `orElse`), higher-order
nodes, expression nodes, and parameters. The editor store
(`ui/packages/rules-core/src/editor.ts`) already exposes the needed mutations:
`replaceNode`, `wrapInOperator`, `addOperand`, `removeOperand`, `unwrap`,
`setDecoration`, `setName`, `setErrors`.

**Consequence:** the "fuller grammar" work is entirely a UI-surface concern.
This design changes **only** the demo app and the sample host. There are **no
changes** to `rules-core`, `rules-react`, `Motiv.Serialization`,
`Motiv.Serialization.AspNetCore`, or any endpoint or wire contract.

## Design Decisions (settled during brainstorming)

| Axis | Decision |
|---|---|
| Primary goal | Run ergonomics (onboarding), serving *both* learn-fast and fork-and-extend equally |
| Content scope | Full: onboarding wrapper **+** presentable styled UI **+** fuller grammar |
| Styling | Vanilla CSS + design tokens (zero new deps), honouring the "agnostic" ethos |
| Builder UX | Inline node toolbar with a **single-open accordion** tree |
| Accordion default | Root fully expanded to a **max depth of 3**; deeper nodes start collapsed |
| Grammar surfaced | spec, AND/OR/XOR, NOT, deep nesting, name + whenTrue/whenFalse decoration |
| Onboarding artifacts | One-command run script, Docker/compose, wiring code comments (**not** a walkthrough README) |
| `run-demo.sh` location | Repo root |
| `toggleNot` helper | Lives in the demo (not promoted to the store) |

## Component Design

### Builder (new: `ui/apps/demo/src/builder/`)

The builder is a recursive accordion tree editor. Each unit has one clear
purpose and communicates through the existing `RuleEditorStore` interface.

- **`RuleNodeEditor`** (recursive) — renders one node: a header row always, plus
  (when the node is expanded) its `DecorationEditor` and/or its children. Owns no
  document state; reads its node via the store/path and calls store mutations.
- **`NodeToolbar`** — the inline action row for a node:
  - Leaf (spec) nodes: spec picker (`<select>` over the catalog), `¬` NOT toggle,
    `×` remove.
  - Operator nodes: `¬` NOT toggle, `⌵` wrap (→ AND / OR / XOR), `＋` add operand,
    `×` remove.
- **`DecorationEditor`** — `name`, `whenTrue`, `whenFalse` inputs, shown when a
  node is expanded. Writes through `setName` / `setDecoration`.
- **Accordion state** — owned by the builder (demo-local UI state, not document
  state). **Single-open:** expanding a node collapses its siblings, preserving
  the holistic view. Initial state: expanded from the root down to
  `MAX_EXPAND_DEPTH = 3`; deeper nodes start collapsed.
- **Collapsed affordances** — a collapsed node still shows its `¬` NOT badge and,
  for operators, an operand count, so structure is scannable without expanding.
- **`toggleNot(path)`** — the only net-new mutation. Built from existing store
  ops: negating wraps the node as `{ not: node }` via `replaceNode`; un-negating
  a `NotNode` uses `unwrap`. Lives in the demo (`builder/mutations.ts` or
  colocated), with a comment noting it could be promoted to the store.

**Grammar surfaced now:** `spec`, `and`, `or`, `xor`, `not`, arbitrary nesting,
and `name` / `whenTrue` / `whenFalse` decoration.

**Deliberately not surfaced (extension points):** `andAlso` / `orElse`,
higher-order nodes, `expression` nodes, and `parameters` editing. The model and
store already support them; a comment at the node-kind switch marks where a
forker adds their UI.

### Styling (new: `ui/apps/demo/src/styles/`)

- **`tokens.css`** — CSS custom properties for colour, spacing, radius, font, and
  semantic roles (surface, border, text, accent, danger). Light/dark handled via
  `@media (prefers-color-scheme: dark)` overriding the token values.
- **Component CSS** — colocated with components or in a single `app.css`, using
  the tokens. **All inline `style={{…}}` usages are removed** from `App`,
  `BuilderPane`, `EvaluatePane`, `JsonPane` and replaced with classes.
- No CSS framework, no new dependency; Vite's built-in CSS handling only.

### Structure (reorganized: `ui/apps/demo/src/`)

```
src/
  builder/        RuleNodeEditor, NodeToolbar, DecorationEditor, mutations
  panes/          BuilderPane, EvaluatePane, JsonPane shells
  styles/         tokens.css, app.css
  App.tsx, main.tsx
```

**Wiring comments at the load-bearing seams only** (joints a forker must
understand, not narration):
- `SpecRegistry` registration and `MapMotivRules` options in the host
  `Program.cs`.
- `RulesApiClient` construction.
- `RuleEditorProvider` / store hookup.

### Run ergonomics

- **`run-demo.sh`** (repo root) + a `Makefile` target: restore, `pnpm` build the
  UI, `dotnet run` the host on fixed port **5100**. Commit `.claude/launch.json`
  for the in-editor preview.
- **Docker** — multi-stage `Dockerfile` (node builds the UI → `dotnet publish` →
  runtime image) and a `docker-compose.yml`, so `docker compose up` runs the
  whole demo with no local .NET/Node toolchain.
- **README** — lean: what it is, one-command run, `docker compose up`, and a
  short "extend it" pointer. No walkthrough wall (the source teaches via the
  seam comments).

## Data Flow

Unchanged from the current demo. The builder mutates the in-memory
`RuleDocument` through `RuleEditorStore`; edits flow through the existing
debounced validation controller (`createValidationController`) to
`POST /validate`; evaluation posts the document + model to `POST /evaluate`; the
catalog comes from `GET /catalog`. The JSON pane reflects the live document.

## Error Handling

Semantics unchanged. The new builder surfaces per-node errors inline using the
store's existing `errorsForNode(errors, path)`. NOT-toggling and decoration edits
re-validate through the same debounced controller. Invalid-model handling
(400 on shape mismatch) is already in place server-side and is untouched.

## Testing

- **Vitest + Testing Library** (`ui/apps/demo/test/`):
  - `RuleNodeEditor`: single-open expand/collapse (opening a node collapses
    siblings), NOT toggle round-trips (`toggleNot` → `NotNode` → back), decoration
    edits reach the document, add/remove operand, max-depth initial expansion.
  - Rendering with vanilla CSS classes (no inline-style assertions).
- **Playwright E2E** (extend the existing spec): build the reference rule via the
  accordion — wrap into an operator, add a `NOT`, name a node — then evaluate and
  assert the resulting justification.
- The existing test suite must stay green.

## Out of Scope

- Editing UI for `andAlso` / `orElse`, higher-order nodes, `expression` nodes,
  and `parameters` (model-supported; left as documented extension points).
- A long annotated walkthrough README.
- Any change to `rules-core` / `rules-react` internals, serialization, or
  endpoints. `toggleNot` is a demo-local helper, not a store change.

## Success Criteria

1. `git clone` → `./run-demo.sh` (or `docker compose up`) → a styled demo running
   at `http://localhost:5100` in one command.
2. The builder can construct `is-adult AND (is-active OR NOT has-orders)` — and
   name/decorate a node — entirely through the accordion UI.
3. No inline styles remain in the demo; the whole UI restyles by editing
   `tokens.css`.
4. A developer can locate every load-bearing seam from the code comments alone.
5. All existing and new tests pass.
