# Rules-Engine Demo → Reference Implementation — Design

**Date:** 2026-07-19 (revised same day)
**Status:** Approved (pending spec review)
**Scope:** `ui/apps/demo`, `src/examples/Motiv.RulesEngine.Sample`, and small additive
changes to `Motiv.Serialization` / `Motiv.Serialization.AspNetCore` / `@motiv/rules-core`
needed to discover registered collections.

> **Revision note.** An earlier draft of this spec targeted "all grammar" on the
> assumption it was purely a UI concern. Reading the backend disproved that:
> higher-order, expression, and parameter support did not exist in the loader.
> Higher-order support has since been built
> (`2026-07-19-higher-order-serialization-design.md`); expression and parameter
> support have not. This revision reconciles the demo to that reality: it surfaces
> the **boolean grammar + higher-order**, and presents **expression and
> parameters as disabled, documented extension points**.

## Problem

The rules-engine demo shipped as a *proof*: four small React files with inline
styles only, a builder covering just `AND`/`OR`/add-operand/remove, and no
styling worth reading. A developer who clones it can see the packages wired
together, but there is nothing they can comfortably **run with** — neither a
clean run-and-learn experience nor a fork-and-extend skeleton.

The goal is to elevate the demo into a **reference implementation**: something a
developer clones, runs in one command, and forks as the starting skeleton of
their own rules UI. It must work equally well as a *learn-fast demo* and a
*fork-and-extend skeleton*.

## Design Decisions (settled during brainstorming)

| Axis | Decision |
|---|---|
| Primary goal | Run ergonomics (onboarding), serving *both* learn-fast and fork-and-extend equally |
| Content scope | Onboarding wrapper **+** presentable styled UI **+** the boolean grammar **+** higher-order |
| Styling | Vanilla CSS + design tokens (zero new deps), honouring the "agnostic" ethos |
| Builder UX | Inline node toolbar with a **single-open accordion** tree |
| Accordion default | Root fully expanded to a **max depth of 5**; deeper nodes start collapsed |
| Grammar surfaced | spec, AND/OR/XOR, andAlso/orElse, NOT, name/whenTrue/whenFalse decoration, and the five **higher-order** quantifiers over registered collections |
| Not-yet-evaluable grammar | `expression` leaves and `parameters` appear as **disabled extension points** (visible, tagged "requires backend", non-functional) |
| Collection discovery | `GET /catalog` is folded to `{ specs, collections }`; the UI reads collections from it |
| Sample host | Enriched with an `Order` collection so higher-order is evaluable end-to-end |
| Onboarding artifacts | One-command run script, Docker/compose, wiring code comments (**not** a walkthrough README) |
| `run-demo.sh` location | Repo root |
| `toggleNot` helper | Lives in the demo (not promoted to the store) |

## Backend Additions (small, additive)

These are the minimum needed for the UI to discover and evaluate higher-order
nodes. No change to the wire semantics of `/validate` or `/evaluate`.

### 1. `SpecRegistry.Collections`

A public enumeration mirroring `Entries`, surfacing each registered collection:

```csharp
public IReadOnlyCollection<CollectionRegistryEntry> Collections { get; }
// CollectionRegistryEntry { Type ParentType; string Path; Type ElementType; }
```

The existing `_collections` dictionary already holds `(parentType, path)` keys
and each `CollectionBinding<TParent>` already exposes `ElementType`; this adds a
public projection over them (and, if cleaner, stores a small entry alongside the
binding). No behavioural change to registration or binding.

### 2. `GET /catalog` folded to `{ specs, collections }`

Today the catalog returns a flat `CatalogEntry[]`. It becomes:

```jsonc
{
  "specs":       [ { "name", "modelType", "metadataType", "isAsync", "description" } ],
  "collections": [ { "path", "parentModelType", "elementModelType" } ]
}
```

- New contract record `CatalogCollection(string Path, string ParentModelType, string ElementModelType)`;
  model ids resolved via `options.ResolveModelId`.
- The catalog handler builds `collections` from `registry.Collections`.
- This is a **breaking response-shape change**; consumers updated in lockstep:
  `@motiv/rules-core` `RulesApiClient.getCatalog` (returns the object), the
  `CatalogEntry` type plus a new `CatalogCollection` type, `useCatalog`, and the
  existing catalog tests (C# `CatalogEndpointTests`, TS client/schema tests).

### 3. Sample host enrichment (`Motiv.RulesEngine.Sample`)

Make higher-order evaluable end-to-end (example code only):

- Add `Order` and give `Customer` an `Orders` collection (defaulting to empty so
  existing `{ age, isActive, orderCount }` sample models still deserialize).
- Register an `Order`-level spec (`is-large-order`).
- `registry.RegisterCollection<Customer, Order>("orders", c => c.Orders)`.

## Component Design (UI)

### Builder (new: `ui/apps/demo/src/builder/`)

A recursive accordion tree editor over the existing `RuleEditorStore`.

- **`RuleNodeEditor`** (recursive) — a header row always; when expanded, its
  `DecorationEditor` and/or children. Reads its node via the store/path; calls
  store mutations.
- **`NodeToolbar`** — inline per-node actions:
  - Leaf (spec) nodes: spec picker (`<select>` over the catalog **scoped to the
    node's model type**), `¬` NOT toggle, `×` remove.
  - Operator nodes: `¬` NOT toggle, `⌵` wrap (AND/OR/XOR/AndAlso/OrElse),
    `＋ operand`, `×` remove.
- **`DecorationEditor`** — `name`/`whenTrue`/`whenFalse` inputs on expand; writes
  through `setName`/`setDecoration`.
- **`QuantifierNode`** — the higher-order node:
  - inserted via a distinct **`＋ add quantifier over a collection`** affordance
    (separate from `＋ operand`, because it opens a child in a *different* model
    space);
  - config on expand: **kind** (AsAllSatisfied / AsAnySatisfied / AsNSatisfied /
    AsAtLeastNSatisfied / AsAtMostNSatisfied), **collection** (dropdown from the
    catalog's `collections`), and **n** (shown only for the three N-kinds);
  - a *"for each {ElementModel}"* band making the model-space switch explicit;
  - a single **element-scoped child** whose spec picker is filtered to catalog
    specs whose `modelType` equals the collection's `elementModelType`;
  - collapsed, it renders a compact badge (e.g. `≥2 of orders`) so the holistic
    scan survives.
- **Accordion state** — demo-local UI state (not document state). **Single-open:**
  expanding a node collapses its siblings. Initial: expanded from the root to
  `MAX_EXPAND_DEPTH = 5`; deeper nodes start collapsed. Collapsed nodes still show
  their `¬` badge / operand count / quantifier badge.
- **Mutations** — existing store ops, plus thin demo-local helpers: `toggleNot`
  (built on `replaceNode`/`unwrap`) and an "insert quantifier" helper that
  produces a higher-order node with a default element-spec child. None require
  `rules-core` changes.
- **Extension points** — `expression` leaves and `parameters` are rendered as
  **disabled** affordances tagged "requires backend (coming)": a forker sees
  where they slot in; nothing constructs a node that would fail to evaluate.

### Styling (new: `ui/apps/demo/src/styles/`)

- **`tokens.css`** — CSS custom properties (colour, spacing, radius, font,
  semantic roles), light/dark via `@media (prefers-color-scheme: dark)`.
- **Component CSS** — colocated or in one `app.css`. **All inline `style={{…}}`
  removed** from `App`/panes and the new builder components.
- No CSS framework, no new dependency.

### Structure (reorganized: `ui/apps/demo/src/`)

```
src/
  builder/   RuleNodeEditor, NodeToolbar, DecorationEditor, QuantifierNode, mutations
  panes/     BuilderPane, EvaluatePane, JsonPane shells
  styles/    tokens.css, app.css
  App.tsx, main.tsx
```

**Wiring comments at the load-bearing seams only:** `SpecRegistry` +
`RegisterCollection` + `MapMotivRules` options (host `Program.cs`),
`RulesApiClient` construction, `RuleEditorProvider`/store hookup.

### Run ergonomics

- **`run-demo.sh`** (repo root) + a `Makefile` target: restore, `pnpm` build the
  UI, `dotnet run` the host on fixed port **5100**. Commit `.claude/launch.json`.
- **Docker** — multi-stage `Dockerfile` (node builds UI → `dotnet publish` →
  runtime) + `docker-compose.yml`, so `docker compose up` needs no local toolchain.
- **README** — lean: what it is, one-command run, `docker compose up`, a short
  "extend it" pointer. No walkthrough wall.

## Data Flow

Unchanged in shape. The builder mutates the in-memory `RuleDocument` via
`RuleEditorStore`; edits flow through the existing debounced validation
controller to `POST /validate`; evaluation posts document + model to
`POST /evaluate`; the catalog (now `{ specs, collections }`) comes from
`GET /catalog`. Element-scoped spec pickers filter the catalog's `specs` by the
selected collection's `elementModelType`.

## Error Handling

Semantics unchanged. Per-node errors surface inline via the store's existing
`errorsForNode`. A quantifier node with an unregistered/edited-away collection
surfaces the backend's `UnknownCollection` error at its path. NOT-toggle,
quantifier config, and decoration edits all re-validate through the same
debounced controller.

## Testing

- **Vitest + Testing Library** (`ui/apps/demo/test/`):
  - `RuleNodeEditor`: single-open expand/collapse, NOT round-trip, decoration
    edits reach the document, add/remove operand, max-depth (5) initial state.
  - `QuantifierNode`: inserting a quantifier writes a higher-order node; kind /
    collection / `n` edits reach the document; the child spec picker is filtered
    to the element model's specs; collapsed badge renders; `n` shows only for the
    N-kinds.
  - Folded catalog: `useCatalog` exposes `specs` and `collections`.
  - Rendering with vanilla CSS classes (no inline-style assertions).
- **Backend**: `CatalogEndpointTests` updated for the `{ specs, collections }`
  shape (specs unchanged + a registered collection surfaced); `SpecRegistry`
  collection-enumeration test.
- **Playwright E2E** (extend the existing spec): build
  `is-adult AND (at least 2 of orders are large)` via the accordion — including
  inserting the quantifier, picking the `orders` collection, setting `n`, and
  choosing the element spec — then evaluate against the enriched `Customer`
  (with orders) and assert the justification.
- All existing and new suites stay green (including the example projects that
  assert justification strings).

## Out of Scope

- `expression` and `parameters` *functional* UI (rendered as disabled extension
  points until their backend sub-projects land).
- A long annotated walkthrough README.
- Changes to `rules-core`/`rules-react` internals or `Motiv.Serialization`
  binding beyond the additive `SpecRegistry.Collections` enumeration and the
  `/catalog` shape. `toggleNot` and the insert-quantifier helper are demo-local.

## Success Criteria

1. `git clone` → `./run-demo.sh` (or `docker compose up`) → a styled demo running
   at `http://localhost:5100` in one command.
2. The builder can construct **and evaluate**, entirely through the accordion:
   boolean compositions (AND/OR/XOR/AndAlso/OrElse, NOT), per-node
   name/whenTrue/whenFalse decoration, and a higher-order quantifier over the
   `orders` collection with an element-scoped child spec.
3. No inline styles remain in the demo; the whole UI restyles by editing
   `tokens.css`.
4. `expression` and `parameters` are visible as clearly-disabled extension points.
5. A developer can locate every load-bearing seam from the code comments alone.
6. All existing and new tests pass across the full solution.
