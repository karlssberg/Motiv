# Rule Externalization (JSON Serialization of Rules) — Design

**Date:** 2026-07-15
**Status:** Approved
**Initiative:** #3 of the post-v8 capability roadmap (async propositions → observability → **rule externalization** → compile-time power)

## Overview

Motiv rules today exist only as compiled C# code. This initiative lets rules be
**externalized as JSON documents** — stored in databases, config files, or
feature-flag systems — and loaded at runtime into fully functional
`SpecBase<TModel, TMetadata>` / `AsyncSpecBase<TModel, TMetadata>` instances,
with the same `Reason` / `Assertions` / `Justification` behavior as fluent-built
specs. Where possible, code-built specs can also be **exported** back to the
JSON format.

The JSON format is a *projection of the fluent builder*, not a new semantic
layer: every document node maps 1:1 to an existing builder operation, so all
established explanation semantics (including the `== true` / `== false` suffix
rules) carry over unchanged.

## Goals

- Author boolean rule composition, explanation text, and metadata in JSON.
- Reference compiled C# specs by stable registered names (the primary leaf
  mechanism), with expression-string leaves as an opt-in escape hatch.
- Support in v1: higher-order propositions, non-string metadata, async
  propositions, and parameterized rules.
- Round-trip where possible: registry-referenced and expression-tree specs
  export faithfully; opaque C# lambdas do not (with a migration-aid stub mode).
- Developer-grade experience by default (clear exceptions with JSON paths),
  with structured validation errors and a published JSON Schema so a
  business-user rules-engine tier can be built on top without friction.

## Non-Goals

- A rule-editing UI or business-user tooling (only the hooks for it).
- Sandboxing expression evaluation against hostile input (registry-only mode
  is the supported answer for untrusted documents).
- Exporting arbitrary C# lambdas or metadata factories to JSON.
- Serializing evaluation *results* (a separate, outbound capability — not this
  initiative).

## Packaging

Two new NuGet packages keep the dependency and security story crisp:

```
Motiv (core, dependency-free)
 └─ Motiv.Serialization
     • SpecRegistry, document model, composition/decoration/higher-order
       loading, parameters, validation, export
     • deps: System.Text.Json only
     └─ Motiv.Serialization.Expressions
         • {"expression": "..."} leaves
         • deps: System.Linq.Dynamic.Core
```

The base package rejects `expression` nodes with a clear error naming the
package to install. Expression support plugs into the base package via an
extension point on `RuleSerializerOptions` (`options.UseExpressions()` defined
in the Expressions package) — the base package has no compile-time knowledge of
the Expressions package.

Target frameworks follow the solution convention (net472, net8.0, net9.0,
net10.0 for tests; the packages themselves target what `Motiv` targets plus
whatever System.Text.Json requires on netfx).

## JSON Document Format

A rule document is a single JSON object: a small envelope plus a tree of rule
nodes.

```json
{
  "$schema": "https://raw.githubusercontent.com/motiv-dotnet/motiv/main/schemas/rule.v1.json",
  "name": "eligible for premium account",
  "parameters": {
    "minAge": { "type": "integer", "default": 18 },
    "minOrders": { "type": "integer" }
  },
  "rule": {
    "andAlso": [
      { "spec": "is-active-customer" },
      { "expression": "Age >= @minAge",
        "whenTrue": "old enough",
        "whenFalse": "too young" },
      { "asAtLeastNSatisfied": { "spec": "is-completed-order" },
        "n": "@minOrders",
        "path": "Orders",
        "whenTrue": "has at least {minOrders} completed orders",
        "whenFalse": "fewer than {minOrders} completed orders" }
    ],
    "name": "premium eligibility"
  }
}
```

### Envelope

| Field | Required | Meaning |
|---|---|---|
| `$schema` | no | Points at the shipped JSON Schema for editor validation/autocomplete. |
| `name` | no | Document-level name; when present, wraps the root node exactly like `.Create("name")`. |
| `parameters` | no | Parameter declarations: `{ "type": ..., "default": ... }`. Types: `integer`, `number`, `string`, `boolean`. A parameter without a `default` is required at load time. |
| `rule` | yes | The root rule node. |

### Node vocabulary

**Leaves**

- `{"spec": "registered-name"}` — references a registry entry.
- `{"expression": "<C#-like boolean expression>"}` — expression-string leaf;
  requires the Expressions package (`ExpressionsNotEnabled` error otherwise).

**Composition** — map 1:1 to Motiv operators, so `Reason`/`Justification`
behave identically to code-built specs:

- `and`, `or`, `xor`, `andAlso`, `orElse` — array of ≥2 nodes; arrays longer
  than 2 fold left (`[a,b,c]` → `(a op b) op c`).
- `not` — single node.

**Decoration** — any node may additionally carry:

- `whenTrue` / `whenFalse` — both-or-neither, and both must be the *same
  kind*: both strings (explanation) or both JSON objects (metadata,
  deserialized to the caller's declared `TMetadata`). Mixing kinds on one
  node is the `MixedWhenTrueFalseKinds` load error. This mirrors the core
  constraint that `WhenTrue`/`WhenFalse` share a `TMetadata`.
- **Per-load kind homogeneity.** A document is loaded either as an
  explanation rule or as a metadata rule, and the whole tree must be
  consistent with that load (see Metadata binding under Loading API). Core
  Motiv composes differing `TMetadata` only by degrading to
  `SpecBase<TModel, string>`, which would silently break a typed metadata
  load — so the loader rejects the mixture instead
  (`MetadataTypeMismatch`, with the node's path).
- `name` — wraps with `.Create("name")` semantics; per core semantics, a name
  demotes `whenTrue`/`whenFalse` strings to metadata (`Values`) and the
  assertion becomes `"name == true"` / `"name == false"`.

**Higher-order** — wrap an inner node evaluated per element:

- `asAllSatisfied`, `asAnySatisfied`, `asNSatisfied`, `asAtLeastNSatisfied`,
  `asAtMostNSatisfied` — the value is the inner (element-model) node.
- `n` — required by the N-parameterized forms; literal integer or `"@param"`.
- `path` — optional property path selecting the collection off the model
  (`"Orders"`, `"Account.Orders"`); omitted when the model itself is the
  collection. Implemented as a `ChangeModelTo` from the document model to the
  collection, composed with the higher-order spec over the element model.

**Parameters** — referenced as `@name` inside expressions and in `n` slots,
and as `{name}` interpolation inside `whenTrue`/`whenFalse` *strings*.
Interpolation happens once at load time (parameters are load-time constants).
`{{` / `}}` escape literal braces.

### JSON Schema

A formal JSON Schema (`schemas/rule.v1.json`) ships in the repo. It encodes
the node vocabulary, envelope shape, and same-kind `whenTrue`/`whenFalse`
constraint as far as JSON Schema allows; semantic rules (unknown spec names,
model-type mismatches) remain loader checks. Every test document validates
against it (see Testing).

## SpecRegistry

A plain catalog of named specs; immutable after construction from the
loader's perspective:

```csharp
var registry = new SpecRegistry()
    .Register("is-active-customer", isActiveCustomer)   // SpecBase<Customer, string>
    .Register("is-completed-order", isCompletedOrder)   // SpecBase<Order, string>
    .Register("has-valid-payment", hasValidPayment);    // AsyncSpecBase<Customer, string>
```

- Names are the stable public contract between code and documents.
- Each entry records its model type, metadata type, and sync/async-ness.
- Registering a duplicate name throws.
- Both `SpecBase<,>` and `AsyncSpecBase<,>` entries are supported.
- The registry also serves reverse lookup (spec instance → name, by reference
  identity) for export.

Type checking happens at load: a document that composes an `Order` spec into a
`Customer` rule fails with an error naming the entry and both types — never at
evaluation.

## Loading API

`RuleSerializer` (name mirrors `JsonSerializer` conventions):

```csharp
var serializer = new RuleSerializer(registry, options);

// Explanation rules (TMetadata = string)
SpecBase<Customer, string> spec = serializer.Deserialize<Customer>(json, parameters);

// Metadata rules — whenTrue/whenFalse objects deserialize to MyMetadata via STJ
SpecBase<Customer, MyMetadata> spec = serializer.Deserialize<Customer, MyMetadata>(json, parameters);

// Async — required if any referenced registry entry is async
AsyncSpecBase<Customer, string> spec = serializer.DeserializeAsyncSpec<Customer>(json, parameters);

// Tooling flow: accumulate every error instead of throwing on the first
IReadOnlyList<RuleError> errors = serializer.Validate(json);                   // structural only
IReadOnlyList<RuleError> errors = serializer.Validate<Customer, MyMetadata>(json); // full semantic
```

- **Validation depth.** `Validate(json)` performs structural checks only
  (node vocabulary, envelope, parameter declarations, same-kind payloads) —
  no model type needed. The generic `Validate<TModel, TMetadata>` overloads
  add the semantic checks (registry lookup, model/metadata type matching,
  expression parsing — expressions can only bind members against a known
  model type).
- **Sync/async split is explicit.** `Deserialize` throws
  (`AsyncSpecInSyncLoad`) if the document references an async registry entry,
  naming it. `DeserializeAsyncSpec` accepts any document, lifting sync leaves
  the same way async mixed composition does. No sync-over-async bridge.
- **Parameters** are `IReadOnlyDictionary<string, object?>` plus an
  anonymous-object convenience overload; merged over document defaults.
  Missing required parameters and surplus parameters are errors.
- **Metadata binding.** In `Deserialize<TModel, TMetadata>` (with
  `TMetadata` ≠ `string`), `whenTrue`/`whenFalse` object payloads deserialize
  to `TMetadata` using the configured `JsonSerializerOptions`, and every node
  in the tree must *resolve to* `TMetadata`: either its referenced registry
  entry already has metadata type `TMetadata`, or the node's own object
  `whenTrue`/`whenFalse` decoration supplies it (re-metadatizing an inner
  string spec, exactly as `Spec.Build(spec).WhenTrue(obj)...` does in core).
  String payloads, and string-metadata registry entries left undecorated, are
  `MetadataTypeMismatch` errors. Conversely, in `Deserialize<TModel>`
  (explanation load), object payloads are `MetadataTypeMismatch` errors.

```json
{
  "name": "premium eligibility",
  "rule": {
    "and": [
      { "spec": "is-active-customer",
        "whenTrue":  { "code": "ACTIVE",   "severity": "info" },
        "whenFalse": { "code": "INACTIVE", "severity": "error" },
        "name": "customer is active" },
      { "spec": "meets-order-quota" }
    ]
  }
}
```

  Loaded as `Deserialize<Customer, RuleOutcome>(json)`: the first node's
  payloads deserialize to `RuleOutcome`, and `meets-order-quota` must be
  registered as a `SpecBase<Customer, RuleOutcome>`.
- **`RuleSerializerOptions`:** the `JsonSerializerOptions` for metadata
  payloads; max document depth / node count; and the expression extension
  point (`UseExpressions()` adds parser config: max expression length, extra
  whitelisted types).

## Export (Round-Trip)

`serializer.Serialize(spec)` walks the composition tree and emits a v1
document:

- **Registry-referenced specs** → `{"spec": "name"}` via reverse lookup by
  reference identity.
- **Expression-tree specs** (`Spec.From(...)` or Expressions-package leaves)
  → `{"expression": "..."}` from their retained lambda source — faithful.
- **Composition / decoration / higher-order** → structural, mirroring load in
  reverse.
- **Opaque C# lambdas** (unregistered `Spec.Build(x => ...)`) — default:
  throw `RuleSerializationException` naming the spec and suggesting
  registration. Opt-in (options flag): emit
  `{"spec": "<statement>", "unresolved": true}` stubs so a rule set can be
  bulk-exported to see exactly which leaves need registering; such documents
  fail validation until resolved.
- **Metadata values** serialize via the configured `JsonSerializerOptions`.
  Metadata *factories* (`WhenTrue(model => ...)`) are functions, not values —
  unexportable, same treatment as opaque lambdas.

**Round-trip guarantee (documented):** `Deserialize(Serialize(spec))` is
behaviorally equivalent for any spec built entirely from registry references,
expressions, composition, decoration, and higher-order operations.

## Motiv.Serialization.Expressions

Contributes exactly one capability: parsing `"Age >= @minAge"` into an
`Expression<Func<TModel, bool>>` using **System.Linq.Dynamic.Core**, after
substituting parameters as typed constants. The resulting expression tree is
handed to Motiv's existing `Spec.From` decomposition, so expression leaves get
per-clause assertions, established `== true/false` behavior, and any future
expression-tree improvements for free.

**Sandboxing defaults:** expressions may access only the model's public
members plus a whitelist of primitive/BCL types (`Math`, `string`,
`DateTime`, ...); no static access beyond the whitelist; no method calls on
arbitrary types. The whitelist is extensible via options.

## Error Model

One exception type, `RuleSerializationException`, carrying a collection of
`RuleError`s. Each `RuleError` has:

- `Path` — JSON path to the offending node (`$.rule.andAlso[1].whenTrue`)
- `Code` — stable code
- `Message` — human-readable, naming the entry/type/parameter involved

Initial codes: `UnknownSpec`, `ModelTypeMismatch`, `MetadataTypeMismatch`,
`MixedWhenTrueFalseKinds`, `MissingParameter`, `SurplusParameter`,
`UnknownParameterReference`, `ExpressionParseFailure`,
`ExpressionsNotEnabled`, `AsyncSpecInSyncLoad`, `InvalidNode`,
`InvalidHigherOrderPath`, `DocumentTooLarge`, `UnresolvedSpecStub`.

`Deserialize` throws on the first error (developer flow); `Validate` returns
all of them (tooling flow). Stable codes + paths are the hook a future
business-user UI keys off.

## Security Posture (documented tiers)

1. **Base package only** — documents can only compose what you registered.
   Safe for untrusted documents by construction.
2. **+ Expressions** — expressions confined to model members + type
   whitelist, with parse-time caps (expression length, document depth/node
   count). Suitable for semi-trusted authors.
3. **Hostile input** — explicitly *not* a supported target for the
   Expressions package; use registry-only.

## AOT Considerations (feeds initiative #4)

- The registry-only path is fully AOT-safe (no runtime codegen; metadata
  binding can use STJ source-gen contexts supplied via options).
- Expression leaves require runtime compilation; where `Compile` is
  unavailable, `preferInterpretation` is the fallback.
- A future source generator could compile documents at build time; the format
  deliberately contains nothing that precludes this.

## Testing Strategy

TDD throughout, per project practice.

- **Format/equivalence tests** — the core idiom: for every node kind, build
  the same rule via JSON and via the fluent builder, and assert identical
  `Satisfied`, `Reason`, `Assertions`, `Justification`, and `Values` across
  representative models. JSON is a projection of the builder; the tests
  enforce that literally.
- **Validation tests** — at least one per error code, asserting `Path` and
  `Code`.
- **Round-trip tests** — serialize→deserialize equivalence for the supported
  surface; opaque-lambda export failure; stub mode.
- **Schema tests** — every valid test document validates against
  `rule.v1.json`; invalid-document tests fail it (keeps the schema honest).
- **net472 runs forced explicitly** — known gotcha: net10-only suites hide
  netfx breaks.
- **Example project** — a realistic rules-engine example under `src/examples`
  (e.g. `Motiv.RulesEngine` + tests), matching the existing examples pattern.

## Implementation Decomposition

Each of these becomes its own implementation plan:

1. **Base package core** — `SpecRegistry`, document model, composition +
   decoration loading, validation/error model, JSON Schema.
2. **Parameters, higher-order, metadata binding.**
3. **Motiv.Serialization.Expressions** — parser integration, sandboxing,
   options extension point.
4. **Export / round-trip.**
5. **Async loading + example project + docs** (README section and `docs/`
   pages per the documentation convention).
