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

// Endpoints
IEndpointRouteBuilder MapMotivRules(this IEndpointRouteBuilder endpoints, string basePath);
IEndpointRouteBuilder MapMotivRules(this IEndpointRouteBuilder endpoints, string basePath,
    SpecRegistry registry, MotivRulesOptions options, RuleSet? rules = null);
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
`{basePath}/rules`:

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
