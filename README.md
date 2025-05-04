# Motiv

![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)

The boolean type has a fundamental flaw: once evaluated,
you lose all context about _why_ the value is true or false.

This is known as _the boolean blindness problem_:

```csharp
// Traditional approach
if (user.Age >= 18 &&
    user.HasValidId &&
    (user.Country == "US" || user.HasInternationalPermit) &&
    !user.IsRestricted)
{
    // Which condition failed? The debugger is your only friend.
}
```

Instead, Motiv preserves the structure of boolean expressions so that when needed, it can determine where the causes
lie:

```csharp
// With Motiv
var canAccess = Spec
    .From((User user) =>
        user.Age >= 18 &
        user.HasValidId &
        (user.Country == "US" || user.HasInternationalPermit) &
        !user.IsRestricted)
    .Create("can access");

var result = canAccess.IsSatisfiedBy(user);
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

var result = isEligible.IsSatisfiedBy(eligableCustomer);
result.Satisfied;  // true
result.Assertions; // ["c.CreditScore > 600", "c.Income > 100000"]
```

This take a lambda expression tree (`Expression<Func<T, bool>>`), and then re-composes it into a
set of sub-propositions.

### Composition
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

var result = isEligible.IsSatisfiedBy(eligableCustomer);
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

var result = hasGoodCredit.IsSatisfiedBy(eligableCustomer);
result.Satisfied;  // true
result.Assertions; // ["has good credit score"]
```

### Collection Logic
Make assertions about sets of results:

```csharp
var allNegative = Spec
    .Build((int n) => n < 0)
    .AsAllSatisfied()
    .WhenTrue("all numbers are negative")
    .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is not negative"))
    .Create();

var result = allNegative.IsSatisfiedBy([-1, 2, 3]);
result.Satisfied;  // false
result.Assertions; // ["2 is not negative", "3 is not negative"]
```

## Quick Start

1. Install:
    ```bash
    dotnet add package Motiv
    ```

2. Use:
    ```csharp
    var spec = Spec
        .From(yourExistingBooleanExpression)
        .Create("human readable name");

    var result = spec.IsSatisfiedBy(data);
    if (!result.Satisfied)
    {
        Console.WriteLine("Failed assertions:");
        foreach (var assertion in result.Assertions)
        {
            Console.WriteLine(assertion);
        }
    }
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
