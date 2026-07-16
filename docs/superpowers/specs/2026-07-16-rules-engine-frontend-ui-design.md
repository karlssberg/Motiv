# Rules Engine Frontend UI — Design

**Date:** 2026-07-16
**Status:** Approved
**Builds on:** 2026-07-15-rule-externalization-design.md (Plan 1 shipped as PR #73)

## Overview

The rule-externalization initiative gave Motiv a JSON rule format
(`rule.v1.json`), a `SpecRegistry`, and a `Validate()` API returning stable
error codes with JSON paths — deliberately leaving "a rule-editing UI or
business-user tooling" as a non-goal, with only the hooks for it. This
initiative builds that tier: a **frontend rules-engine library** that talks to
a Motiv-driven backend, delivered as a full vertical slice — HTTP contract,
ASP.NET Core reference backend, headless TypeScript core, React reference
adapter, and a demo app proving the loop end-to-end.

### Personas served

1. **Business users** — visually compose/edit rules from the registry catalog.
2. **Developers** — author rule JSON with live validation and rule-tree
   visualization.
3. **Result viewers** — read-only rendering of evaluation results
   (`Reason` / `Assertions` / `Justification`) to see *why* a rule passed or
   failed.

### Agnosticism goals (decided)

- **Frontend-framework agnostic** — headless TypeScript core + thin framework
  adapters (the "TanStack model"); React is the reference adapter.
- **Backend/transport agnostic** — the frontend targets an OpenAPI-described
  contract, not a specific server; the ASP.NET Core package is the reference
  implementation.
- **Design-system agnostic** — headless/unstyled components; consumers bring
  their own markup and CSS.
- **Not** rules-format agnostic: the UI is purpose-built for Motiv's
  `rule.v1.json` grammar.

### Architecture decision

Three approaches were considered: (A) headless TS core + framework adapters,
(B) Web Components (Lit), (C) protocol-only with a demo app. **A was chosen**:
it best serves the framework- and design-system-agnosticism goals, keeps ~80%
of the logic in a framework-free unit-testable core, and follows an
industry-proven pattern (TanStack, Zag.js, Radix). B's shadow-DOM theming is
too coarse for design-system agnosticism; C delivers no reusable library.

## Non-Goals

- Persistence/CRUD of rule documents (storage, auth, and versioning are the
  host application's concern; the UI treats documents as values).
- UI support for parameters, higher-order nodes, metadata-object payloads, or
  expression leaves in the first slice (they enter as backend Plans 2–4 land;
  the contract leaves room for them).
- Full result-serialization capability (only the minimal DTO the `/evaluate`
  endpoint needs is pulled forward).
- Non-React adapters (enabled by the core, not built yet).

## Backend

### HTTP contract (transport-agnostic)

Source of truth: **OpenAPI 3.1** document at `schemas/rules-api.v1.yaml`,
alongside `rule.v1.json`. Three endpoints:

| Endpoint | Request | Response |
|---|---|---|
| `GET {base}/catalog` | — | Registry entries: `name`, `modelType`, `metadataType`, `isAsync`, optional `description` |
| `POST {base}/validate` | rule document + model/metadata type ids | `{ "errors": [{ "path", "code", "message" }] }` |
| `POST {base}/evaluate` | rule document + sample model JSON + parameters | serialized evaluation result (below) |

- `/validate` maps directly onto the existing `RuleSerializer.Validate()`
  overloads (structural when no model type is given, semantic when it is).
- `modelType` ids are stable strings the host chooses (see model
  registration below), decoupling the contract from CLR type names.

### `Motiv.Serialization.AspNetCore` package

Minimal-API integration:

```csharp
app.MapMotivRules("/api/rules", registry, options);
```

- `options.AddModel<Customer>("customer")` — registers evaluable model types;
  `/evaluate` binds incoming model JSON to the CLR type via this map, and the
  ids appear as `modelType` in the catalog.
- Spec registration gains an optional human-readable `description` surfaced
  in the catalog (the palette text a business-user builder displays).
- Targets net8.0+ only (minimal APIs preclude net472; the netfx story stays
  with the transport-agnostic contract, which anyone can implement).

### Result serialization (in `Motiv.Serialization`)

A minimal `ResultSerializer` — the pulled-forward slice of the previously
deferred "outbound capability" — producing:

```json
{
  "satisfied": true,
  "reason": "(is active == true) & (age >= 18 == true)",
  "assertions": ["is active == true", "age >= 18 == true"],
  "values": [ "..." ],
  "justification": "AND\n    is active == true\n    ...",
  "explanation": { "assertions": ["..."], "underlying": [ { "...": "recursive" } ] }
}
```

`explanation` mirrors Motiv's causal `Explanation` graph (de-noised, causes
only), so UIs render an interactive justification tree without parsing the
indented `justification` string. `values` serialize via the configured
`JsonSerializerOptions`.

### Slice scope guard

The slice supports exactly what Plan 1 loads today: registry leaves;
`and` / `or` / `xor` / `andAlso` / `orElse` / `not` composition;
`whenTrue` / `whenFalse` / `name` decoration; explanation (string-metadata)
rules. Nothing in the contract shapes precludes parameters, higher-order
nodes, metadata objects, or expressions later.

## Frontend

### Workspace layout

pnpm workspace at the repo root:

```
ui/
├── packages/
│   ├── rules-core/     → @motiv/rules-core   (framework-free)
│   └── rules-react/    → @motiv/rules-react  (reference adapter)
└── apps/
    └── demo/           → Vite React demo (not published)
```

### `@motiv/rules-core` — zero runtime dependencies

- **`types`** — `RuleDocument` / `RuleNode` discriminated union mirroring
  `rule.v1.json`, plus catalog / error / result types mirroring the OpenAPI
  contract. Hand-written for ergonomics; a CI test validates shared fixture
  documents against the shipped JSON Schema so types and schema cannot drift.
- **`client`** — `RulesApiClient` over the three endpoints. Injectable
  `fetch` + base URL keep auth, proxies, and non-browser runtimes outside the
  library (the backend-agnosticism seam).
- **`editor`** — an internal observable store (subscribe/getState; no state
  library) over an immutable document tree. Operations: insert / remove /
  replace node, wrap-in-operator, unwrap, set `whenTrue` / `whenFalse` /
  `name`; undo/redo via snapshot history. Orchestrates validation: debounced
  `client.validate()` after each edit, indexing returned errors by JSON path
  so any node looks up its own errors in O(1) — consuming exactly the stable
  `path` + `code` error model the backend was designed to emit.
- **`explanation`** — walks the `/evaluate` response's `explanation` tree
  into a render-friendly model (depth, satisfied flags, expand/collapse
  helpers).

### `@motiv/rules-react` — thin, headless

- Hooks: `useRuleEditor(store)`, `useRuleNode(path)` (value + errors + edit
  callbacks), `useCatalog(client)`, `useEvaluation(client)`.
- Headless components for the structurally hard parts:
  `<RuleEditorProvider>`, `<RuleTree>` / `<RuleNodeSlot>` (render-prop based —
  consumer supplies markup, component supplies state and ARIA
  `tree`/`treeitem` wiring), `<JustificationTree>` likewise. No CSS shipped.

### Demo app (vertical-slice proof)

One page, three panes, served by a small ASP.NET Core sample host using
`MapMotivRules` with a handful of registered specs:

1. **Builder** — styled business-user editor: catalog palette,
   and/or/not grouping, whenTrue/whenFalse text fields.
2. **JSON view** — live document with inline validation errors.
3. **Evaluate** — sample-model JSON in, rendered explanation tree out.

Each pane exercises one persona (business user, developer, result viewer).

## Testing Strategy

- **Backend** — TDD/xunit per project convention: contract tests per
  endpoint; `ResultSerializer` equivalence tests against fluent-built
  results; net472 runs forced explicitly where applicable.
- **`@motiv/rules-core`** — Vitest: store operations, undo/redo, error-path
  indexing, client against mocked `fetch`.
- **`@motiv/rules-react`** — Testing Library component tests.
- **Contract drift guard** — shared JSON fixtures under `schemas/fixtures/`
  consumed by *both* the .NET and TypeScript test suites.
- **End-to-end** — one Playwright smoke test through the demo: build rule →
  validate → evaluate.
- **CI** — a Node job (pnpm install / build / test) alongside the existing
  .NET workflow.

## Implementation Decomposition

Each becomes its own implementation plan:

1. **Result serialization** — `ResultSerializer` + DTO in
   `Motiv.Serialization`.
2. **HTTP contract + ASP.NET Core package** — `rules-api.v1.yaml`,
   `MapMotivRules`, model registration, catalog descriptions.
3. **`@motiv/rules-core`** — workspace scaffolding, types + schema-drift
   test, client, editor store, explanation model.
4. **`@motiv/rules-react` + demo app + E2E** — hooks, headless components,
   sample host, three-pane demo, Playwright smoke, CI Node job.
