<img src="https://raw.githubusercontent.com/karlssberg/Motiv/main/icon.png" alt="Motiv logo" width="64" align="left"/>

# Motiv

![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)

Motiv is a .NET library for building composable, explainable boolean logic — so you never lose the _why_ behind a true or false.

The boolean type has a problem: once evaluated,
you lose all context about _why_ the value is true or false.

This is known as _the boolean blindness problem_:

```csharp
// Traditional approach - life before Motiv
if (user.Age >= 18 &&
    user.HasValidId &&
    (user.Country == "US" || user.HasInternationalPermit) &&
    !user.IsRestricted)
{
    // Access granted
}
else
{
    // Access denied — but which condition failed?
}
```

Motiv addresses this by preserving the structure of boolean expressions, so you can recover the underlying causes when you need them:

```csharp
// With Motiv
var canAccess = Spec
    .From((User user) =>
        user.Age >= 18 &
        user.HasValidId &
        (user.Country == "US" | user.HasInternationalPermit) &
        !user.IsRestricted)
    .Create("can access");

var result = canAccess.Evaluate(user);
result.Satisfied;  // false
result.Assertions; // ["user.Age < 18", "user.HasValidId == false"]
```

Motiv overloads `&`, `|`, `^`, and `!` so the same operators compose propositions and their results.
The short-circuiting `&&` / `||` are reserved for evaluated results — use `.AndAlso()` / `.OrElse()` on propositions.
Notice too that each failing clause is rendered in its own terms — `user.Age < 18` for the comparison,
`user.HasValidId == false` for the boolean — and passing clauses are dropped from the result.

## Core Features

### Automatic Propositions

Transform boolean expressions into explanatory logic using the `Spec.From()` method:

```csharp
var isEligible = Spec
    .From((Customer c) => c.CreditScore > 600 & c.Income > 100000)
    .Create("eligible for loan");

var result = isEligible.Evaluate(eligibleCustomer);
result.Satisfied;  // true
result.Assertions; // ["c.CreditScore > 600", "c.Income > 100000"]
```

This takes a lambda expression tree (`Expression<Func<T, bool>>`) and transforms it into a hierarchy of propositions that mirror the expression's logic.

### Manual Composition

For full control, compose propositions manually — no expression trees:

```csharp
var hasGoodCredit = Spec
    .Build((Customer c) => c.CreditScore > 600)
    .Create("good credit");

var hasIncome = Spec
    .Build((Customer c) => c.Income > 100000)
    .Create("sufficient income");

// create a new proposition
var isEligible = hasGoodCredit.And(hasIncome);

// alternatively, use operator syntax
// var isEligible = hasGoodCredit & hasIncome;

var result = isEligible.Evaluate(eligibleCustomer);
result.Satisfied;  // true
result.Assertions; // ["good credit == true", "sufficient income == true"]
                   // a bare name gets a == true / == false suffix to show the outcome
```

### Custom Assertions

Add readable explanations to your logic:

```csharp
var hasGoodCredit = Spec
    .Build((Customer c) => c.CreditScore > 600)
    .WhenTrue("has good credit score")
    .WhenFalse("credit score too low")
    .Create();

var result = hasGoodCredit.Evaluate(eligibleCustomer);
result.Satisfied;  // true
result.Assertions; // ["has good credit score"]
```

Supplying an explicit name via `Create("name")` instead of parameterless `Create()` changes the semantics: the name plus
a `== true`/`== false` suffix becomes the assertion, and the custom strings become metadata, available via `Values`:

```csharp
var hasGoodCredit = Spec
    .Build((Customer c) => c.CreditScore > 600)
    .WhenTrue("has good credit score")
    .WhenFalse("credit score too low")
    .Create("good credit");

var result = hasGoodCredit.Evaluate(eligibleCustomer);
result.Satisfied;  // true
result.Assertions; // ["good credit == true"]
result.Values;     // ["has good credit score"]
```

### Query Provider Integration

Propositions built from `Spec.From()` retain a recoverable expression tree,
so they compose into a single predicate that a query provider can translate directly:

```csharp
var isAdult  = Spec.From((Customer c) => c.Age >= 18).Create("is adult");
var isActive = Spec.From((Customer c) => c.IsActive).Create("is active");

var eligible = isAdult & isActive;

// Translate to SQL via any IQueryable provider (e.g. EF Core)
var customers = dbContext.Customers.Where(eligible);

// Or take the raw expression anywhere expressions are accepted
Expression<Func<Customer, bool>> predicate = eligible.ToExpression();
```

### Asynchronous Propositions

Compose rules that touch databases, APIs, or feature flags — with the same
explainable results and true short-circuiting of asynchronous work:

```csharp
var isAdult = Spec
    .Build((User u) => u.Age >= 18)
    .Create("is adult");

var hasCredit = Spec
    .BuildAsync(async (User u, CancellationToken ct) =>
        await creditApi.CheckAsync(u.Id, ct))
    .WhenTrue("has credit")
    .WhenFalse("no credit")
    .Create();

var canBuy = isAdult.AndAlso(hasCredit);   // credit API never called for minors

var result = await canBuy.EvaluateAsync(user, cancellationToken);
result.Satisfied;  // false
result.Assertions; // ["is adult == false"]
```

Async and sync propositions compose freely (sync operands are lifted
automatically), and independent async operands can opt into concurrent
evaluation with `AndConcurrently`/`OrConcurrently`/`XOrConcurrently`.

### Side-Effect Observers

Attach logging, metrics, or other side-effects without altering a proposition's behavior:

```csharp
var observed = isEligible
    .TapWhenTrue((customer, result) =>
        logger.LogInformation("Approved: {Id}", customer.Id))
    .TapWhenFalse((customer, result) =>
        logger.LogWarning("Denied: {Reason}", result.Reason));

// Use exactly like the original — result, assertions, reason are all unchanged
var result = observed.Evaluate(customer);
```

### Observability

Every top-level evaluation reports through OpenTelemetry — a span plus counter/histogram metrics —
with no Motiv configuration required. Nothing is emitted until your application subscribes:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MotivTelemetry.SourceName))
    .WithMetrics(metrics => metrics.AddMeter(MotivTelemetry.MeterName));
```

### Collection Logic

Make assertions about collections of items (also known as higher-order logic):

```csharp
var allNegative = Spec
    .Build((int n) => n < 0)
    .AsAllSatisfied()
    .WhenTrue("all numbers are negative")
    .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is not negative"))
    .Create();

var result = allNegative.Evaluate([-1, 2, 3]);
result.Satisfied;  // false
result.Assertions; // ["2 is not negative", "3 is not negative"]
```

### Live Rules

Hot-swap a running application's rules without redeploying. Declare a rule as a
sealed class — the type is its identity — with a compiled spec (or a JSON rule
document) as its default implementation:

```csharp
public sealed class CanCheckoutRule() : Rule<Customer, string>(
    "can-checkout", CanCheckoutSpec, "Gate for the checkout flow");
```

Wire the rule up and inject the concrete type wherever the decision is made:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRule<CanCheckoutRule>();

app.MapMotivRules("/api/rules");

app.MapPost("/api/checkout", (CanCheckoutRule canCheckout, Customer customer) =>
    Results.Json(canCheckout.Evaluate(customer).Assertions));
```

`PUT /api/rules/rules/can-checkout` replaces the implementation live — every
evaluation reads an immutable snapshot, writes are protected by optimistic
concurrency (`409` on a stale `baseVersion`), and `DELETE` reverts to the
default. Available via the `Motiv.Serialization` and
`Motiv.Serialization.AspNetCore` packages.

## Quick Start

Install the Motiv NuGet package:

```bash
dotnet add package Motiv
```

or via the NuGet Package Manager:

```bash
Install-Package Motiv
```

## Technical Notes

- Zero additional dependencies on .NET 8+
  - The legacy `netstandard2.0` target pulls in `System.Diagnostics.DiagnosticSource` for telemetry
- Metadata is evaluated lazily
- Compatible with both .NET and .NET Framework
- Zero-allocation fast paths for boolean-only evaluation
- MIT licensed

## Learn More

- [Documentation](https://karlssberg.github.io/Motiv/)
- [Try Online](https://dotnetfiddle.net/knykpD)
- [GitHub](https://github.com/karlssberg/Motiv/)
