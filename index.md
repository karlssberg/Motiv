# Know _Why_, not just _What_

[![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg)](https://github.com/karlssberg/Motiv)
[![GitHub](https://img.shields.io/github/license/karlssberg/Motiv)](https://github.com/karlssberg/Motiv/blob/main/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/)
[![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)
[![GitHub Repo stars](https://img.shields.io/github/stars/karlssberg/Motiv)](https://github.com/karlssberg/Motiv)

Motiv is a developer-first .NET library that transforms the way you work with boolean logic.
It lets you form expressions from discrete [propositions](https://en.wikipedia.org/wiki/Proposition) so that you
can explain _why_ decisions were made.

First create [atomic propositions](https://en.wikipedia.org/wiki/Atomic_sentence):

```csharp
// Define propositions
var isValid = Spec.Build((int n) => n is >= 0 and <= 11).Create("valid");
var isEmpty = Spec.Build((int n) => n == 0).Create("empty");
var isFull  = Spec.Build((int n) => n == 11).Create("full");
```

Then compose using operators (e.g.,`!`, `&`, `|`, `^`):

```csharp
// Compose a new proposition
var isPartiallyFull = isValid & !(isEmpty | isFull);
```

To get detailed feedback:

```csharp
// Evaluate the proposition
var result = isPartiallyFull.IsSatisfiedBy(5);

result.Satisfied;   // true
result.Assertions;  // ["valid", "!empty", "!full"]
```

## Installation

Motiv is available as a [NuGet Package](https://www.nuget.org/packages/Motiv/).
Install it using one of the following methods:

**NuGet Package Manager Console:**
```bash
Install-Package Motiv
```

**.NET CLI:**
```bash
dotnet add package Motiv
```

## Basic Usage

Let's start with a simple example to demonstrate Motiv's core concepts:

```csharp
// Define a basic proposition
var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

// Evaluate the proposition
var result = isEven.IsSatisfiedBy(2);

result.Satisfied;   // true
result.Reason;      // "is even"
result.Assertions;  // ["is even"]
```

This minimal example showcases how easily you can create and evaluate propositions with Motiv.

## Advanced Usage

### Explicit Assertions

For more descriptive results, use `WhenTrue()` and `WhenFalse()` to define custom assertions:

```csharp
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("is even")
        .WhenFalse("is odd")
        .Create();

var result = isEven.IsSatisfiedBy(3);

result.Satisfied;  // false
result.Reason;     // "is odd"
result.Assertions; // ["is odd"]
```

### Custom Metadata

For scenarios requiring more context, you can use metadata instead of simple string assertions:

```csharp
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue(new MyMetadata("is even"))
        .WhenFalse(new MyMetadata("is odd"))
        .Create("is even");

var result = isEven.IsSatisfiedBy(2);

result.Satisfied;  // true
result.Reason;     // "is even"
result.Metadata;   // [{ Text: "is even" }]
```

### Composing Propositions

Motiv's true power shines when composing complex logic from simpler propositions. Here's an example solving the classic [Fizz Buzz](https://en.wikipedia.org/wiki/Fizz_buzz) problem:

```csharp
var isFizz = Spec.Build((int n) => n % 3 == 0).Create("fizz");
var isBuzz = Spec.Build((int n) => n % 5 == 0).Create("buzz");

var isSubstitution = 
    Spec.Build(isFizz | isBuzz)
        .WhenTrue((_, result) => string.Concat(result.Assertions))
        .WhenFalse(n => n.ToString())
        .Create("is substitution");

isSubstitution.IsSatisfiedBy(15).Reason;  // "fizzbuzz"
isSubstitution.IsSatisfiedBy(3).Reason;   // "fizz"
isSubstitution.IsSatisfiedBy(5).Reason;   // "buzz"
isSubstitution.IsSatisfiedBy(2).Reason;   // "2"
```

This example demonstrates how you can compose complex propositions from simpler ones using Motiv.

---

## When to Use Motiv

Motiv is not meant to replace all your boolean logic.
You should only use it when it makes sense to do so.
If your logic is pretty straightforward or does not really need any feedback about the decisions being made, then 
you might not see a big benefit from using Motiv.
It is just another tool in your toolbox, and sometimes the simplest solution is the best fit.

Consider using Motiv when you need two or more of the following:

1. **Visibility**: Granular, real-time feedback about decisions
2. **Decomposition**: Break down complex logic into meaningful subclauses
3. **Reusability**: Avoid logic duplication across your codebase
4. **Modeling**: Explicitly model your domain logic (e.g., for 
   [Domain-Driven Design](https://en.wikipedia.org/wiki/Domain-driven_design))
5. **Testing**: Test your logic in isolation—without mocking dependencies

### Tradeoffs

1. **Performance**: Motiv is not designed for high-performance scenarios where every nanosecond counts.
   Its focus is on maintainability and readability, although in most use-cases the performance overhead is negligible.
2. **Dependency**: Once embedded in your codebase, removing Motiv can be challenging.
   However, it does not depend on any third-party libraries itself, so it won't bring any unexpected baggage.
3. **Learning Curve**: New users may need time to adapt to Motiv's approach and API
