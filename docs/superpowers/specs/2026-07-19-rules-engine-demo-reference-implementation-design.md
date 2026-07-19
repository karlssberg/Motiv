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
| Accordion default | Root fully expanded to a **max depth of 5**; deeper nodes start collapsed |
| Grammar surfaced | **All node kinds**: spec, expression, AND/OR/XOR, andAlso/orElse, NOT, all five higher-order nodes, name + whenTrue/whenFalse decoration, and document parameters |
| Sample host | Enriched so every surfaced grammar feature is evaluable end-to-end (a collection for higher-order, an expression-enabled model, a declared parameter) — example-host only, no library changes |
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
  - Leaf nodes: a **type toggle** between *spec reference* (`<select>` over the
    catalog) and *expression* (a text input for a lambda string), plus `¬` NOT
    toggle and `×` remove.
  - Operator nodes: `¬` NOT toggle, `⌵` wrap, `＋` add operand, `×` remove.
  - The **wrap menu** (`⌵`) offers every combinator: the binary operators
    AND / OR / XOR / AndAlso / OrElse, and the five higher-order forms
    (AsAllSatisfied, AsAnySatisfied, AsNSatisfied, AsAtLeastNSatisfied,
    AsAtMostNSatisfied).
- **`DecorationEditor`** — `name`, `whenTrue`, `whenFalse` inputs, shown when a
  node is expanded. Writes through `setName` / `setDecoration`.
- **`HigherOrderFields`** — for higher-order nodes, an inline editor for the
  count `n` (the three N-variants) as either a literal number or a `@param`
  reference (a dropdown of declared parameters), and the optional `path`
  (advanced text input, commented as a pointer to the collection sub-path).
- **`ParametersEditor`** — a document-level panel (above the tree) to declare
  `RuleDocument.parameters`: name, type (`integer` / `number` / `string` /
  `boolean`), and optional default. Declared parameters populate the `@param`
  dropdown in `HigherOrderFields`.
- **Accordion state** — owned by the builder (demo-local UI state, not document
  state). **Single-open:** expanding a node collapses its siblings, preserving
  the holistic view. Initial state: expanded from the root down to
  `MAX_EXPAND_DEPTH = 5`; deeper nodes start collapsed.
- **Collapsed affordances** — a collapsed node still shows its `¬` NOT badge and,
  for operators, an operand count, so structure is scannable without expanding.
- **`toggleNot(path)`** — the only net-new mutation. Built from existing store
  ops: negating wraps the node as `{ not: node }` via `replaceNode`; un-negating
  a `NotNode` uses `unwrap`. Lives in the demo (`builder/mutations.ts` or
  colocated), with a comment noting it could be promoted to the store.

**Grammar surfaced:** the complete document grammar — `spec`, `expression`,
`and` / `or` / `xor` / `andAlso` / `orElse`, `not`, arbitrary nesting, all five
higher-order nodes (`asAllSatisfied`, `asAnySatisfied`, `asNSatisfied`,
`asAtLeastNSatisfied`, `asAtMostNSatisfied`) with their `n` / `path`, document
`parameters`, and `name` / `whenTrue` / `whenFalse` decoration on every node.

Some store mutations may need thin demo-local helpers alongside `toggleNot`
(e.g. wrapping a node in a higher-order form, or converting a leaf between spec
and expression). Any such helper is built from existing store ops
(`replaceNode` / `wrapInOperator` / `unwrap`) and lives in the demo; none require
changes to `rules-core`.

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
  builder/        RuleNodeEditor, NodeToolbar, DecorationEditor,
                  HigherOrderFields, ParametersEditor, mutations
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

### Sample host domain (make all grammar evaluable)

The current host registers three scalar specs over a flat `Customer`. That
cannot exercise higher-order (which needs a collection) or expression nodes
(which need an expression-enabled model), so the builder could construct nodes
that fail on evaluate. The reference must let a developer build **and run** every
grammar feature. The sample domain is enriched accordingly — **example-host code
only, no library or endpoint changes:**

- Add an **`Order`** type and give `Customer` a collection of orders, plus an
  order-level spec (e.g. `is-large-order`) so the higher-order nodes have a
  collection to operate over via `path`.
- Ensure an **expression-enabled** registration path so `expression` nodes
  compile against the model (configured through `RuleSerializerOptions` /
  `MotivRulesOptions`). The first plan task **verifies** expression evaluation
  end-to-end and adjusts host configuration if required.
- Seed the demo document with a **declared parameter** (e.g. an `integer`
  threshold) referenced by an N-variant higher-order node, so `@param` wiring is
  demonstrable out of the box.

If the end-to-end verification surfaces a genuine gap in the serialization layer
(rather than host configuration), that is treated as a separate finding and
raised before expanding scope — this design assumes the wire contract already
supports every kind, consistent with `RuleNode.cs` and `RuleDocumentParser`.

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
store's existing `errorsForNode(errors, path)`. NOT-toggling, higher-order/`n`
edits, expression edits, and decoration edits all re-validate through the same
debounced controller. Invalid-model handling (400 on shape mismatch) is already
in place server-side and is untouched.

## Testing

- **Vitest + Testing Library** (`ui/apps/demo/test/`):
  - `RuleNodeEditor`: single-open expand/collapse (opening a node collapses
    siblings), NOT toggle round-trips (`toggleNot` → `NotNode` → back), decoration
    edits reach the document, add/remove operand, max-depth (5) initial expansion.
  - Combinators: wrap into each binary operator (AND/OR/XOR/AndAlso/OrElse) and
    each higher-order form; the `n` editor accepts a literal and a `@param`; the
    leaf type toggle switches a node between `spec` and `expression`.
  - `ParametersEditor`: declaring a parameter reaches `RuleDocument.parameters`
    and populates the `@param` dropdown.
  - Rendering with vanilla CSS classes (no inline-style assertions).
- **Playwright E2E** (extend the existing spec): build a rule that exercises the
  full grammar — declare a parameter, wrap operands in a higher-order node using
  that `@param`, add a `NOT`, name a node — then evaluate against the enriched
  model and assert the resulting justification.
- The existing test suite must stay green.

## Out of Scope

- A long annotated walkthrough README (source teaches via the seam comments).
- Any change to `rules-core` / `rules-react` internals, `Motiv.Serialization`,
  `Motiv.Serialization.AspNetCore`, or the wire contract. All new mutation
  helpers (`toggleNot` and any wrap/convert helpers) are demo-local and built on
  existing store ops. Sample-host domain enrichment is example code, not a
  library change.

## Success Criteria

1. `git clone` → `./run-demo.sh` (or `docker compose up`) → a styled demo running
   at `http://localhost:5100` in one command.
2. The builder can construct **and evaluate** rules using every grammar feature —
   binary operators, short-circuit operators, NOT, all five higher-order forms
   (with a `@param` count), expression leaves, and per-node name/whenTrue/whenFalse
   decoration — entirely through the accordion UI.
3. No inline styles remain in the demo; the whole UI restyles by editing
   `tokens.css`.
4. A developer can locate every load-bearing seam from the code comments alone.
5. All existing and new tests pass.
