---
title: ASP.NET Core Integration
---

`AddPropositions()` extends the [live-rules wiring](../live-rules/AspNetCore.md) with a
[`PropositionSet`](PropositionSet.md) and mounts six endpoints under the same route group.

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddPropositions(new JsonFilePropositionStore("propositions.json"))
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>();

var app = builder.Build();
app.MapMotivRules("/api/rules");
```

## AddPropositions()

```csharp
MotivRulesBuilder AddPropositions(IPropositionStore? store = null);
```

- Registers the [`IPropositionStore`](IPropositionStore.md), defaulting to
  `InMemoryPropositionStore`.
- Replays every `MotivRulesOptions.AddModel<TModel>()` registration onto the set, so the id a client
  passes as `modelType` is the same one the evaluate and validate endpoints use.
- Calls `Load()` &mdash; stored propositions bind **before** rule defaults do, so a rule's
  compiled-in default document may reference one.
- Builds the set against the same coordinator as the `RuleSet`, so a proposition edit and a rule
  update can never interleave.

Call it once. Calling it twice silently keeps only the last registration, as DI is last-wins.

## Endpoints

Mounted under `{basePath}/propositions`:

| Verb | Path | Body / query | Success | Failure |
|---|---|---|---|---|
| `GET` | `{basePath}/propositions` | &mdash; | `200` the effective listing | &mdash; |
| `GET` | `{basePath}/propositions/{name}` | &mdash; | `200 { document, version, origin, hasCompiledDefault }` | `404` |
| `POST` | `{basePath}/propositions` | `{ name, modelType, document, description? }` | `201 { version }` | `400`, `409` name taken |
| `PUT` | `{basePath}/propositions/{name}` | `{ document, baseVersion }` | `200 { version }` | `400`, `409 { currentVersion }`, `404` |
| `DELETE` | `{basePath}/propositions/{name}` | `?baseVersion=n` | `200 { version: 0 }` | `400`, `409 { referrers }`, `404` |
| `GET` | `{basePath}/propositions/{name}/dependents` | &mdash; | `200 { dependents }` transitive closure | `404` |

Each listing entry carries `name`, `modelType`, `metadataType`, `isAsync`, `origin`
(`Compiled` &vert; `Overridden` &vert; `Authored`), `version`, `description` and `quarantine`.

### Why create is POST

`PUT` and `DELETE` already reserve `baseVersion` as strictly positive &mdash; versions start at 1
&mdash; leaving no spare value meaning "expect absent" without overloading a field that has one
clear meaning today. `POST` needs no `baseVersion`, because there is nothing yet to conflict with.

**`POST` is also how an override is created.** A `409` name-taken therefore means precisely *an
authored document already exists* under that name; a name that exists only as a compiled spec is
accepted, and the resulting override starts at version 1.

### The three 409s are distinct

They are told apart by their bodies, not by the status:

| Body | Meaning |
|---|---|
| `{ "currentVersion": n }` | the caller's `baseVersion` was stale |
| `{ "error": "A proposition is already authored under '…'." }` | the name is taken |
| `{ "referrers": ["…"] }` | removal refused; those still reference it |

### DELETE answers the same for revert and remove

Both report `200 { "version": 0 }`. Which one happened is not in the response, so read the
proposition's `origin` (or `hasCompiledDefault`) **before** the call: an `Overridden` proposition is
reverted and the name survives, served by the compiled spec; an `Authored` one is removed and the
name is gone.

### Broken dependents are reported apart from document errors

A `400` from `POST`/`PUT`/`DELETE` carries both lists, and they mean different things:

```jsonc
{
  "errors": [],
  "brokenDependents": [
    { "name": "can-checkout", "kind": "rule", "errors": [ /* RuleError[] */ ] }
  ]
}
```

`errors` are faults at a JSON pointer inside *this* document. `brokenDependents` are documents
somewhere else that stopped binding because of this edit &mdash; a path into this document could not
address them. Nothing was published either way.

## GET /catalog Reflects Authored Propositions

The catalog is a projection of the layered source, so a proposition authored a moment ago appears in
it immediately, tagged with its origin. Without this the builder's spec picker could not offer
authored propositions as operands, and composability is the whole feature.
