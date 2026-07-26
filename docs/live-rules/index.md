---
title: Live Rules
description: Documentation for live rules in Motiv, which wrap serialized rule documents in typed, hot-swappable rule handles that a running application can replace without redeploying.
---

Live rules turn serialized rule documents into **typed, hot-swappable decision handles**. An
application declares each rule as a sealed class, injects the concrete type wherever the decision is
made, and evaluates it like any other proposition &mdash; while the implementation behind the handle
can be replaced at runtime, through HTTP endpoints or directly through a
[`RuleSet`](RuleSet.md), without a restart and without tearing in-flight evaluations.

Live rules ship in the `Motiv.Serialization` package; the HTTP endpoints and DI wiring ship in
`Motiv.Serialization.AspNetCore`.

## Declaring Rules

A rule is a sealed subclass of one of the [four rule classes](Rules.md), so the type itself is the
identity &mdash; no name strings at the call site:

```csharp
public sealed class CanCheckoutRule() : Rule<Customer, string>(
    "can-checkout", DefaultSpecs.CanCheckout, "Gate for the checkout flow");
```

The four flavours mirror Motiv's spec/policy × sync/async matrix:

| Class                                | Evaluates via                        | Result                                        | Guarantees                          |
|----------------------------------------|----------------------------------------|-------------------------------------------------|---------------------------------------|
| `Rule<TModel, TMetadata>`               | `Evaluate(model)`                      | `BooleanResultBase<TMetadata>`                  | Spec-flavoured; may yield many values |
| `PolicyRule<TModel, TMetadata>`         | `Evaluate(model)`                      | `PolicyResultBase<TMetadata>`                   | Exactly one value per evaluation      |
| `AsyncRule<TModel, TMetadata>`          | `EvaluateAsync(model, ct)`             | `ValueTask<BooleanResultBase<TMetadata>>`       | Async; may yield many values          |
| `AsyncPolicyRule<TModel, TMetadata>`    | `EvaluateAsync(model, ct)`             | `ValueTask<PolicyResultBase<TMetadata>>`        | Async; exactly one value              |

## Defaults

Every rule has a default implementation &mdash; the version-1 behavior it starts on and the behavior
`DELETE`/`Revert` restores. A default is either:

- **A compiled spec** &mdash; a `SpecBase`/`PolicyBase` (or async equivalent) built in code, passed
  directly to the rule constructor. Never needs deserializing, so it cannot fail to bind.
- **A rule document** &mdash; JSON from [`RuleDocuments.FromJson()` or
  `RuleDocuments.Embedded()`](RuleDocuments.md), bound against the registry when the rule is added
  to a [`RuleSet`](RuleSet.md). An invalid default document **fails at startup** (with the failing
  rule's name in the exception), never at first evaluation.

## Concurrency Model

Evaluations and updates never block each other:

- **Evaluations read an immutable snapshot.** Each `Evaluate()`/`EvaluateAsync()` call captures the
  current implementation once; a concurrent update can never tear an in-flight evaluation, which
  always completes against a coherent version.
- **Writers get optimistic concurrency.** Every update carries the version the writer last observed;
  a compare-and-swap publish means a stale writer receives a version conflict (HTTP `409`, or
  `RuleUpdateOutcome.VersionConflict`) instead of silently clobbering another writer's change.
- **Validate → bind → publish.** A replacement document is fully validated and bound *before* it is
  published; an invalid document leaves the live rule untouched.
- **Versions only move forward.** Versions start at 1 and increment on every successful update *and*
  every revert &mdash; reverting to the default is a new version, not a rollback of the counter.

## Wiring and Endpoints

In ASP.NET Core, [`AddMotivRules()` enrolls rules into DI and `MapMotivRules()` mounts the
endpoints](AspNetCore.md):

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>();

var app = builder.Build();
app.MapMotivRules("/api/rules");
```

This maps the rule-management endpoints under `{basePath}/rules`:

| Endpoint                          | Purpose                                                                |
|-------------------------------------|--------------------------------------------------------------------------|
| `GET {basePath}/rules`              | Lists every rule: name, model, metadata type, flavour, version, description. |
| `GET {basePath}/rules/{name}`       | One rule's current document and version, from a single coherent snapshot. |
| `PUT {basePath}/rules/{name}`       | Replaces the implementation: `{ document, baseVersion }` → `200 { version }`, `409 { currentVersion }`, or `400 { errors }`. |
| `DELETE {basePath}/rules/{name}`    | Reverts to the default (`?baseVersion=n`), same outcome contract as `PUT`. |

Outside ASP.NET Core (or without DI), the same lifecycle is available directly on
[`RuleSet`](RuleSet.md):

```csharp
var rules = new RuleSet(registry).Add(new CanCheckoutRule());
```

## Async Loading Boundary

Async rules bind their documents via
[`RuleSerializer.DeserializeAsyncSpec()`](DeserializeAsyncSpec.md), which composes a mixed sync/async
registry into a single `AsyncSpecBase`:

- **Sync references are lifted** into the async hierarchy (via the same mechanism as
  [`ToAsyncSpec()`](../async/ToAsyncSpec.md)); **async references are used directly**.
- **Higher-order subtrees must be fully synchronous.** A quantifier (`asAllSatisfied`,
  `asAnySatisfied`, …) binds its subtree synchronously and lifts the whole result; an async spec
  referenced inside one is rejected with the `AsyncSpecInHigherOrder` error.
- **Policy rules reject spec-shaped documents.** Updating a `PolicyRule`/`AsyncPolicyRule` with a
  document whose root binds to a spec (rather than a policy) fails with the `PolicyRequired` error.

## Available Types and Methods

| Page                                              | Description                                                                       |
|-----------------------------------------------------|-------------------------------------------------------------------------------------|
| [Rule Classes](Rules.md)                            | `Rule`, `PolicyRule`, `AsyncRule`, `AsyncPolicyRule` — declaring and evaluating rules. |
| [RuleSet](RuleSet.md)                               | Registering rules, binding defaults at startup, `Update()`/`Revert()` with optimistic concurrency. |
| [RuleDocuments](RuleDocuments.md)                   | `FromJson()` and `Embedded()` — document sources for rule defaults.               |
| [ASP.NET Core Integration](AspNetCore.md)           | `AddMotivRules()`, `AddRule()`, `MapMotivRules()`, and the HTTP endpoint contract. |
| [DeserializeAsyncSpec()](DeserializeAsyncSpec.md)   | Loading rule documents into the async hierarchy, and the sync/async boundary rules. |

## Next Steps

- See [Rule Classes](Rules.md) for the four flavours and their evaluation surfaces.
- See [ASP.NET Core Integration](AspNetCore.md) for wiring rules into a web application.
- Read about [Asynchronous Propositions](../async/index.md), the hierarchy async rules evaluate over.
