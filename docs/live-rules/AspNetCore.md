---
title: ASP.NET Core Integration
---

The `Motiv.Serialization.AspNetCore` package wires live rules into an ASP.NET Core application:
`AddMotivRules()` enrolls rules as DI singletons and builds the [`RuleSet`](RuleSet.md);
`MapMotivRules()` mounts the HTTP endpoints that read and replace them.

```csharp
// Registration
MotivRulesBuilder AddMotivRules(this IServiceCollection services, SpecRegistry registry, MotivRulesOptions options);
MotivRulesBuilder AddRule<TRule>() where TRule : RuleBase, new();
MotivRulesBuilder AddRule<TRule>(TRule rule) where TRule : RuleBase;

// Options — registers a model under a stable id, the one clients pass as `modelType`
MotivRulesOptions AddModel<TModel>(string modelTypeId);

// Endpoints
IEndpointRouteBuilder MapMotivRules(this IEndpointRouteBuilder endpoints, string basePath,
    Action<MotivRulesEndpointOptions>? configureEndpoints = null);
IEndpointRouteBuilder MapMotivRules(this IEndpointRouteBuilder endpoints, string basePath,
    SpecRegistry registry, MotivRulesOptions options, RuleSet? rules = null,
    Action<MotivRulesEndpointOptions>? configureEndpoints = null);
```

## Wiring

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>()
    .AddRule<LoyaltyDiscountRule>();

var app = builder.Build();

app.MapMotivRules("/api/rules");
```

## A complete host

The wiring above in full &mdash; the smallest program that serves live rules. Everything a real
deployment adds (a durable store, identity, namespace grants, an approval gate, a decision log) is
layered on top of exactly this, and none of it changes these lines:

```csharp
using Motiv;
using Motiv.Serialization;
using Motiv.Serialization.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// The spec catalog: rule documents reference specs by these names, and the names
// are what the authoring UI lists.
var registry = new SpecRegistry()
    .Register(
        "customer.is-active",
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active")
            .WhenFalse("customer is inactive")
            .Create(),
        "Whether the customer account is active")
    .Register(
        "customer.is-adult",
        Spec.Build((Customer c) => c.Age >= 18)
            .WhenTrue("customer is an adult")
            .WhenFalse("customer is a minor")
            .Create(),
        "Whether the customer is 18 or older");

// Evaluable models: each id is the string clients pass as `modelType`.
var options = new MotivRulesOptions().AddModel<Customer>("customer");

builder.Services.AddMotivRules(registry, options)
    .AddRule<CanCheckoutRule>();

var app = builder.Build();

// Secure by default — see Security below for the opt-out.
app.MapMotivRules("/api/rules");

// The rule being *used*: inject the concrete type and evaluate it.
app.MapPost("/api/checkout", (CanCheckoutRule canCheckout, Customer customer) =>
    Results.Json(new { approved = canCheckout.Evaluate(customer).Satisfied }));

app.Run();

public sealed class CanCheckoutRule() : Rule<Customer, string>(
    "can-checkout",
    Spec.Build((Customer c) => c.IsActive).Create("customer is active"),
    "Gate for the checkout flow");

public record Customer(bool IsActive, int Age);
```

Rules live for the process lifetime until a store is added &mdash; `PUT` a document and the next
request executes it, but a restart returns every rule to its compiled default. See
[Durability](durability.md) for `AddRuleStore()` and the
[EF Core store](entity-framework-store.md) for the reference implementation.

**`src/Motiv.Studio` is the worked example.** It is the flagship rules-governance app rather than a
tutorial, so it is the place to read how these endpoints are hosted for real: an EF Core-backed
store, a fail-closed development identity, namespace grants from three interchangeable sources, an
approval gate, and a durable decision log &mdash; each marked with a `Seam:` comment at the point
where an adopter would substitute their own.

## Security

The mapped group is **secure by default** &mdash; every route under `{basePath}` requires an
authenticated caller (`RequireAuthorization()`). Opening it up is an explicit, greppable opt-out at
the mount site, never a silent default:

```csharp
app.MapMotivRules("/api/rules", options => options.AllowAnonymous());
```

Layer [namespace grants](../governance/grants.md) on top to control *what* an authenticated caller may
read and write, and [`AddGovernance()`](../governance/change-requests.md) for a review gate in front
of publishing &mdash; both opt-in, and both mount additional routes (`/change-requests`, `/gate`) under
this same secured group when enabled. See [Governance](../governance/index.md) for the full pipeline.

Each `AddRule<TRule>()` registers the rule as a singleton under its concrete type and enrolls it in
the `RuleSet`. Inject the concrete type wherever the rule is executed:

```csharp
app.MapPost("/api/checkout", async (
    CanCheckoutRule canCheckout,
    FraudScreeningRule fraudScreening,
    Customer customer,
    CancellationToken cancellationToken) =>
{
    var eligibility = canCheckout.Evaluate(customer);
    var screening = await fraudScreening.EvaluateAsync(customer, cancellationToken);
    return Results.Json(new { approved = eligibility.Satisfied && screening.Satisfied });
});
```

## Endpoints

`MapMotivRules(basePath)` maps the document endpoints (`GET {basePath}/catalog`,
`POST {basePath}/validate`, `POST {basePath}/evaluate`) plus the rule-management endpoints under
`{basePath}/rules`.

`POST {basePath}/validate` takes `{ modelType, document, isAsync? }`. Set `isAsync: true` to
validate for an asynchronous load — the document may then reference async specs, mirroring
[`DeserializeAsyncSpec`](DeserializeAsyncSpec.md); without it, an async spec reference is
reported as `AsyncSpecInSyncLoad`.

The rule-management endpoints:

| Method & path                        | Request                          | Responses                                                                    |
|----------------------------------------|-------------------------------------|--------------------------------------------------------------------------------|
| `GET {basePath}/rules`                 | &mdash;                             | `200` &mdash; array of `{ name, modelType, metadataType, isAsync, isPolicy, version, description }` |
| `GET {basePath}/rules/{name}`          | &mdash;                             | `200 { document, version }` (document is `null` on a compiled default); `404`  |
| `PUT {basePath}/rules/{name}`          | `{ document, baseVersion }`         | `200 { version }`; `409 { currentVersion }`; `400 { errors }`; `404`           |
| `DELETE {basePath}/rules/{name}`       | `?baseVersion=n`                    | `200 { version }` (reverted to the default); `409 { currentVersion }`; `400 { errors }`; `404` |

`baseVersion` is the version the writer last observed &mdash; the optimistic-concurrency token. A
`409` means another writer published first; re-`GET` the rule to adopt the current version before
retrying. A `400` carries the document's binding errors (path, code, message) and leaves the live
rule untouched.

## Catalog type schemas

`GET {basePath}/catalog` also carries two JSON Schema maps, generated once at `MapMotivRules` time:

```json
{
  "specs": [...],
  "collections": [...],
  "metadataTypes": { "String": { "type": ["string", "null"] }, "Verdict": { ... } },
  "modelTypes": { "customer": { "type": ["object", "null"], "properties": { "age": ... } } }
}
```

- **Keying.** `metadataTypes` is keyed by the same `metadataType` strings the spec and rule listings
  already carry (the CLR type's simple name, e.g. `"String"`, `"Verdict"`); its keys are the union
  of the registry entries' and the mounted rules' metadata types. `modelTypes` is keyed by the
  registered model id (e.g. `"customer"`) &mdash; the same string `validate`/`evaluate` take as
  `modelType`.
- **Options parity.** Each schema is exported with the exact `JsonSerializerOptions` that kind is
  deserialized with: metadata payloads (`whenTrue`/`whenFalse`) use
  `MotivRulesOptions.SerializerOptions.MetadataJsonOptions` (STJ defaults: exact-case property
  names), models use `MotivRulesOptions.JsonSerializerOptions` (web defaults: camelCase). Property
  names in the schemas therefore match real binding behavior by construction &mdash; a value that
  conforms to the schema binds, and there is no second naming convention to keep in sync.
- **Numbers may be typed `["string", "integer"]`.** The web-default options allow numbers to be
  read from JSON strings, so the exporter describes numeric model properties as a
  string-or-number union with a `pattern` constraining the string form (e.g.
  `{"type": ["string", "integer"], "pattern": "^-?(?:0|[1-9]\\d*)$"}`). `"30"` is genuinely
  accepted by the binder; a frontend validator should honor the union rather than flag it.

Frontends can enforce these client-side before submitting &mdash; `@motiv-rules/core` ships a
matching structural validator (`validateAgainstSchema`). Both maps are additive; clients written
against older hosts should treat them as optional and skip enforcement when absent.

## Remarks

- **Invalid defaults fail at startup.** `MapMotivRules(basePath)` resolves the `RuleSet` eagerly,
  which binds every enrolled rule's default &mdash; a rule with an invalid default document throws
  at startup, naming the rule, rather than at first request.
- **One binding authority.** The DI overload maps the endpoints with the same registry and
  serializer options the `RuleSet` was built with, so the validate/evaluate endpoints and the
  rule-update endpoints can never disagree on how documents bind.
- **Rules bind on first resolve.** An enrolled rule is only bound once the `RuleSet` is resolved
  (which `MapMotivRules(basePath)` does); evaluating it before then throws.
- **The non-DI overload is explicit.** `MapMotivRules(basePath, registry, options, rules)` takes the
  `RuleSet` directly (or `null` to omit the rule endpoints); construct it with the same registry and
  `MotivRulesOptions.SerializerOptions` so bindings agree.

## Next Steps

- Declare the rules being enrolled with the [Rule Classes](Rules.md).
- See [`RuleSet`](RuleSet.md) for the `Update()`/`Revert()` semantics behind `PUT`/`DELETE`.
- See the [Live Rules overview](index.md) for the concurrency model these endpoints rely on.
- See [Governance](../governance/index.md) for authentication, namespace grants, and the approval gate
  layered on top of this surface.
