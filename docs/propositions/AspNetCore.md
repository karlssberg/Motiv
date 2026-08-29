---
title: ASP.NET Core Integration
---

`AddPropositions()` extends the [live-rules wiring](../live-rules/AspNetCore.md) with a
[`PropositionSet`](PropositionSet.md) and mounts six endpoints under the same route group.
`JsonFilePropositionStore` below is Studio's own
[`IPropositionStore`](IPropositionStore.md) implementation, not a library type &mdash; durability
stays outside the library, exactly as transport does. Omit the argument to use
`InMemoryPropositionStore`.

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

Call it once. A second call throws `InvalidOperationException` rather than letting DI's last-wins
registration quietly discard the first store.

These endpoints are mounted under the same group as the rule endpoints, which is **secure by
default**: `MapMotivRules()` calls `RequireAuthorization()` on the whole group unless the host opts
out with `AllowAnonymous()`. `POST /propositions` can override a compiled spec, which changes what
*every* rule referencing that name evaluates &mdash; layer [namespace grants](../governance/grants.md)
on top so only callers holding a `Publish` grant on the target namespace can write, and
[`AddGovernance()`](../governance/change-requests.md) for a review gate before the write lands. See
[Governance](../governance/index.md) for the full pipeline.

## Endpoints

Every `document` these endpoints accept is a **composition of specs that already exist**: each leaf
must be a `spec` reference, because `expression` leaves do not bind at runtime and a predicate is
C#. There is no empty proposition to `POST`. See
[Composition Only](index.md#composition-only).

Mounted under `{basePath}/propositions`:

| Verb | Path | Body / query | Success | Failure |
|---|---|---|---|---|
| `GET` | `{basePath}/propositions` | &mdash; | `200` the effective listing | &mdash; |
| `GET` | `{basePath}/propositions/{name}` | &mdash; | `200 { document, version, origin, hasCompiledDefault }` | `404` |
| `POST` | `{basePath}/propositions` | `{ name, modelType, document, description? }` | `201 { version }` | `400`, `409` name taken |
| `PUT` | `{basePath}/propositions/{name}` | `{ document, baseVersion }` | `200 { version }` | `400`, `409 { currentVersion }`, `404` |
| `DELETE` | `{basePath}/propositions/{name}` | `?baseVersion=n` | `200 { version: 0 }` | `400`, `409 { currentVersion }`, `409 { referrers }`, `404` |
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

They are told apart by their bodies, not by the status. `PUT` and `DELETE` can each answer the
version-conflict shape; only `POST` answers name-taken and only `DELETE` answers referrers:

| Body | Meaning |
|---|---|
| `{ "currentVersion": n }` | the caller's `baseVersion` was stale |
| `{ "error": "A proposition is already authored under '…'." }` | the name is taken |
| `{ "referrers": ["…"] }` | removal refused; those still reference it |

### DELETE answers the same for revert and remove

Both report `200 { "version": 0 }`. Which one happened is not in the response, so read the
proposition's `origin` **before** the call: an `Overridden` proposition is reverted and the name
survives, served by the compiled spec; an `Authored` one is removed and the name is gone.

`hasCompiledDefault` alone does not answer this, because it is true for a purely `Compiled`
proposition too &mdash; and there is no authored document to withdraw, so DELETE answers `404`.
Read it together with `origin`: reverts are `Overridden`, removals are `Authored`.

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

A quarantined **authored** proposition is the one exception: it resolves to nothing, so it is
omitted from `/catalog` and cannot be offered as an operand. A quarantined **override** still
appears, reported as the compiled spec resolving beneath it. Both remain in `GET /propositions`
with their `quarantine` errors, which is where they are repaired or deleted.

## Next Steps

- See [PropositionSet](PropositionSet.md) for the write path these endpoints call and its outcome
  contract.
- See [IPropositionStore](IPropositionStore.md) for where authored documents persist.
- See [Runtime Propositions](index.md) for the cascade, quarantine and naming rules behind the
  contract.
- See [Live Rules: ASP.NET Core Integration](../live-rules/AspNetCore.md) for the rule endpoints
  mounted under the same base path.
