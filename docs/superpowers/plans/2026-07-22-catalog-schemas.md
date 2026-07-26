# Catalog Type Schemas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `/catalog` response exposes JSON Schemas for every metadata type and every registered model type, so frontends can enforce the shape of `whenTrue`/`whenFalse` payloads and model JSON before submitting.

**Architecture:** Schemas are generated at `MapMotivRules` time (once, like the existing cached catalog) via `System.Text.Json.Schema.JsonSchemaExporter` in **Motiv.Serialization.AspNetCore only** (net10 — exporter is in-box; Motiv.Serialization untouched). Each kind uses the options it is actually deserialized with: metadata payloads → `RuleSerializerOptions.MetadataJsonOptions ?? JsonSerializerOptions.Default` (exact-case today), models → `MotivRulesOptions.JsonSerializerOptions` (camelCase web defaults) — schema property names therefore match real binding behavior by construction. The catalog gains two deduplicated maps keyed by the strings entries already carry: `metadataTypes` (keyed by the existing `metadataType` name, e.g. `"String"`, `"Verdict"`) and `modelTypes` (keyed by registered model id, e.g. `"customer"`). Metadata keys are the union of registry entries' and (when a RuleSet is mounted) rules' metadata types. rules-core gains the contract types plus a small structural validator covering the exporter's POCO subset; the demo wires it to its model-JSON textareas for inline feedback.

**Tech Stack:** C# (Motiv.Serialization.AspNetCore + tests), TypeScript (rules-core + demo, vitest).

**Test commands:** `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.AspNetCore.Tests -v minimal` · `cd ui && pnpm -r test`

---

### Task 1: Catalog schema maps (backend)

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/RulesContracts.cs` — `CatalogResponse` gains `IReadOnlyDictionary<string, JsonElement> MetadataTypes` and `ModelTypes` (XML docs stating the keying and the options-parity guarantee).
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesOptions.cs` — internal enumeration of `(Id, ModelType)` bindings (the `_bindings` dictionary is private today).
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` — at map time build both maps: metadata types = distinct `registry.Entries[*].MetadataType` ∪ `rules?.Rules[*].MetadataType`, keyed `Type.Name` (matching the existing `metadataType` strings); model types from the options bindings keyed by id. Generate via `JsonSchemaExporter.GetJsonSchemaAsNode(optionsForKind, type)` → `JsonElement`. Metadata options: `options.SerializerOptions?.MetadataJsonOptions ?? JsonSerializerOptions.Default`; model options: `options.JsonSerializerOptions`.
- Test: `src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs` — extend: catalog contains `metadataTypes` with `"String"` (`{"type":["string","null"]}`-shaped or whatever the exporter emits — pin actual output on first run per CLAUDE.md) and a POCO metadata type whose properties use the metadata options' casing; `modelTypes` keyed by registered id with camelCase properties (proving the options split); a rule-only metadata type (registered via RuleSet, absent from registry entries) appears in the map.

Steps: failing tests → implement → filtered + full AspNetCore.Tests + serialization tests pass → commit `feat: catalog exposes metadata and model JSON Schemas`.

Notes: `JsonSerializerOptions.Default` may need `MakeReadOnly()`/reflection resolver considerations — if the exporter requires a resolver-bearing options instance, construct `new JsonSerializerOptions()` with the default reflection resolver instead; pin from actual behavior. Sort map keys ordinally for stable output.

### Task 2: rules-core contracts + structural validator

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts` — `Catalog` gains `metadataTypes: Record<string, JsonSchema>` and `modelTypes: Record<string, JsonSchema>`; add a minimal `JsonSchema` structural interface (`type`, `properties`, `required`, `items`, `enum` — the exporter's POCO subset; index-signature-friendly for unknown keywords).
- Create: `ui/packages/rules-core/src/schema.ts` — `validateAgainstSchema(value: unknown, schema: JsonSchema): SchemaViolation[]` covering: `type` (string | string[] incl. `"null"` and `"integer"`), `properties` recursion, `required`, `items`, `enum`. Unknown keywords ignored (permissive-by-default). `SchemaViolation { path: string; message: string }`.
- Modify: `ui/packages/rules-core/src/index.ts` re-exports.
- Tests: `ui/packages/rules-core/test/schema.test.ts` (accept/reject per keyword, nested paths, type unions) + extend `test/client.test.ts`/catalog fixture types if they break.

Steps: failing tests → implement → `pnpm --filter @motiv/rules-core test` + build + typecheck + `pnpm -r test` → commit `feat(rules-core): catalog type schemas and structural validator`.

### Task 3: Demo enforcement + docs

**Files:**
- Modify: `ui/apps/demo/src/panes/EvaluatePane.tsx` and `CheckoutPane.tsx` — validate the JSON textarea against the relevant `modelTypes` schema from the catalog (`customer` model) on evaluate/try; violations render in the existing error/alert style (path + message) and block the request; invalid JSON keeps its current handling. Keep changes minimal — reuse `useCatalog`.
- Tests: extend `ui/apps/demo/test/panes/{EvaluatePane,CheckoutPane}.test.tsx` — a schema violation (e.g. `"age": "thirty"`) shows the violation and does not fetch.
- Docs: `docs/live-rules/AspNetCore.md` — catalog schema maps section; `ui/apps/demo/README.md` — one paragraph.

Steps: failing tests → implement → demo tests + `pnpm -r test` + full dotnet solution → commit `feat(demo): enforce model schemas on evaluate and checkout input`.

### Task 4: Verify + review
Full solution + workspace + e2e recipe green; code-simplifier pass over the changed files; push to PR #79.
