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

The rules stack reports on itself on a second source and meter (`MotivRulesTelemetry.SourceName`/`MeterName`):
bind failures, publish conflicts, store latency, replica lag, decision-queue depth and break-glass, plus a span
carrying which rule ran at which version. Stating a decision-log capture posture also sets the PII posture for
traces, so it is stated once.

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

### Runtime Propositions

Propositions are the building blocks rules are made of. Register them in C#, or
author them at runtime and persist them server-side — either way a rule document
references them by name:

```csharp
builder.Services.AddMotivRules(registry, options)
    // JsonFilePropositionStore is the sample host's own IPropositionStore, not a library type
    .AddPropositions(new JsonFilePropositionStore("propositions.json"))
    .AddRule<CanCheckoutRule>();
```

```jsonc
// POST /api/rules/propositions
{
  "name": "customer.eligibility.is-eligible",
  "modelType": "customer",
  "document": {
    "rule": { "andAlso": [{ "spec": "customer.is-active" }, { "spec": "customer.is-adult" }] }
  }
}
```

Names are namespaced with dots, an authored document may override a compiled spec
(and `DELETE` reverts to it), and editing a proposition rebinds every rule and
proposition that references it — transactionally, so an edit that would break a
dependent is refused whole. Authored propositions are *composition only*: they
combine specs that already exist, because new primitive facts still come from C#.
Available via the `Motiv.Serialization` and `Motiv.Serialization.AspNetCore`
packages.

### Rule Durability

Register a store so a published rule survives a restart instead of reverting to
its compiled default:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRuleStore(new JsonFileRuleStore("rules.json"))
    .AddRule<CanCheckoutRule>();
```

Every publish appends an immutable, provenance-carrying row to an append-only
version log — who published, when, and why — rather than overwriting the last
one, so history is auditable and a rollback (`RestoreAsync`) appends a fresh
copy of an old version instead of rewriting it. A stored document that no
longer binds after a redeploy is *quarantined* — the rule keeps running its
compiled default rather than failing to evaluate — and, by default, stops
startup so nobody boots quietly into unapproved behaviour. Available via the
`Motiv.Serialization` and `Motiv.Serialization.AspNetCore` packages.

### Entity Framework Core Store

`Motiv.Serialization.EntityFrameworkCore` backs `IRuleStore` and
`IPropositionStore` with a real database instead of a JSON file &mdash; SQLite,
PostgreSQL or SQL Server:

```csharp
builder.Services.AddMotivEntityFrameworkStore(options =>
    options.UseSqlite("Data Source=motiv-store.db"));

builder.Services.AddMotivRules(registry, options)
    .AddPropositions(provider => new EfPropositionStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddRuleStore(provider => new EfRuleStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddRule<CanCheckoutRule>();
```

The `(Name, Version)` primary key is enforced by the database, so two replicas
racing a publish really do produce one `200` and one `409` rather than both
reading a stale file. Development calls `EnsureCreatedAsync()`; production
derives `MotivStoreDbContext`, registers it with
`AddMotivEntityFrameworkStore<AppStoreDbContext>(...)` and owns its migrations,
the same split `Microsoft.AspNetCore.Identity.EntityFrameworkCore` draws. A `StoreImport`
helper carries history in, once, from a pre-existing `JsonFileRuleStore` /
`JsonFilePropositionStore` pair. Available via the
`Motiv.Serialization.EntityFrameworkCore` package.

### Multi-Instance Refresh

A durable store survives a restart, but a running replica never rereads it on
its own — two replicas can otherwise diverge for as long as they're both up.
`AddRefresh()` polls a cheap generation and rebuilds this replica whenever
another one has published:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRuleStore(new JsonFileRuleStore("rules.json"))
    .AddRule<CanCheckoutRule>()
    .AddRefresh(); // opt-in — a single-replica host doesn't need it
```

Each rebuild is a whole-world swap, not an in-place patch, and aborts rather
than silently regressing a live rule to its compiled default if a stored
document would no longer bind — the replica keeps serving what it has and
reports `Degraded` via the `motiv-refresh` health check until it's repaired.
`MapMotivRules` pins one world per request automatically, so a handler
evaluating several rules can't straddle a concurrent refresh, and every
response carries a `Motiv-Generation` header so a client can tell it was
routed to a replica serving an older world. Available via the
`Motiv.Serialization` and `Motiv.Serialization.AspNetCore` packages.

### The Decision Log

Motiv builds a full explanation on every evaluation and then discards it. Mark
a rule `audited` in its document and every evaluation is recorded instead — so
you can answer *why this customer was declined, on the 3rd, at 14:07*:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddDecisionLog(new InMemoryDecisionSink(), log =>
        // No default: a rule marked audited over a model type with no posture
        // registered here will not bind. ReferenceOnly keeps a key and nothing
        // else, so erasure and audit can coexist.
        log.Capture.ReferenceOnly<Customer>(customer => customer.CustomerId))
    .AddRule<CanCheckoutRule>();
```

The flag lives on the *document*, so it's versioned, toggling it is a governed
change, and a rule on a compiled default can't claim to be audited — it has
nowhere to put the flag. Each `DecisionRecord` pins behaviour with three
anchors (the rule's version, the build, and the versions of every authored
proposition it resolved through), carries the full justification, and keeps
only what your chosen capture posture allows of the model. Records leave the
evaluation path through a bounded queue drained into an `IDecisionSink` — your
seam for a durable table, a SIEM, or an outbox — and a full queue fails the
decision by default, because an audited decision that wasn't logged didn't
happen.

For production, `SqlDecisionSink` appends to a database of its own — separate
from the authoring store, over SQLite, PostgreSQL or SQL Server, with no
provider dependency of its own:

```csharp
builder.Services.AddSingleton(_ => new SqlDecisionSink(
    () => new SqliteConnection(decisionsConnectionString),
    new SqlDecisionSinkOptions
    {
        Dialect = DecisionSqlDialect.Sqlite,
        // Required. Version history is kept forever; an audited rule on a hot
        // path is millions of rows, so there is no "keep everything" here.
        Retention = TimeSpan.FromDays(90)
    }));
```

It refuses to be constructed without a retention window and purges past it on a
loop it starts itself — a purge you can forget to register is an unbounded
table. Available via the `Motiv.Serialization`,
`Motiv.Serialization.AspNetCore` and `Motiv.Serialization.Sql` packages.

### Governance and Access Control

Live rules are secure by default: `MapMotivRules()` requires authentication on
the whole endpoint group, and opening it up is an explicit opt-out
(`AllowAnonymous()`), never a silent default. Layer `AddGovernance()` on top for
a maker-checker gate that a publish must satisfy:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddGovernance() // permissive until a gate document is installed
    .AddRule<CanCheckoutRule>();

app.MapMotivRules("/api/rules");
// or: app.MapMotivRules("/api/rules", o => o.AllowAnonymous());
```

```jsonc
// PUT /api/rules/gate — publish requires an approval, and never from the author
{
  "document": {
    "rule": { "and": [
      { "spec": "change.approver-count-at-least", "args": { "n": 1 } },
      { "not": { "spec": "change.author-is-approver" } }
    ]}
  }
}
```

An unapproved publish refuses with the same explainability the library exists
to provide:

```jsonc
// 403 from POST /api/rules/change-requests/{id}/publish
{
  "reason": "change has fewer than 1 approvals",
  "assertions": ["change has fewer than 1 approvals"],
  "justification": "AND\n    change has fewer than 1 approvals"
}
```

The gate's default is permissive and namespace grants (`IGrantSource`) are
opt-in, so enabling either changes no response until it is configured.
Available via the `Motiv.Serialization` and `Motiv.Serialization.AspNetCore`
packages.

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
