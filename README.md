<img src="https://raw.githubusercontent.com/karlssberg/Motiv/main/icon.png" alt="Motiv logo" width="64" align="left"/>

# Motiv

![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)

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
    // Which condition failed? How do I debug this?
}
```

Motiv addresses this by preserving the structure of boolean expressions, allowing you to identify the underlying causes when needed:

```csharp
// With Motiv
var canAccess = Spec
    .From((User user) =>
        user.Age >= 18 &
        user.HasValidId &
        (user.Country == "US" || user.HasInternationalPermit) &
        !user.IsRestricted)
    .Create("can access");

var result = canAccess.Evaluate(user);
result.Satisfied;  // false
result.Assertions; // ["user.Age < 18", "user.HasValidId == false"]
```

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

Alternatively, if you want full control, you can do this yourself:

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
result.Assertions; // ["good credit", "sufficient income"]
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

## Quick Start

Installation involves adding the Motiv NuGet package to your project:

```bash
dotnet add package Motiv
```

or via the NuGet Package Manager:

```bash
Install-Package Motiv
```

## Technical Notes

- Zero additional dependencies
- Metadata lazily evaluated
- .NET & .NET Framework compatible
- Performance optimized
- MIT licensed

## Learn More

- [Documentation](https://karlssberg.github.io/Motiv/)
- [Try Online](https://dotnetfiddle.net/knykpD)
- [GitHub](https://github.com/karlssberg/Motiv/)
