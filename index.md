---
title: Supercharge your boolean logic with Motiv
layout: home
---

[![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg)](https://github.com/karlssberg/Motiv) [![NuGet](https://img.
shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)


Motiv is a .NET library that enables you to write more expressive, maintainable, and testable boolean logic.
Its fluent API allows you to effortlessly create and compose 
[propositions](https://en.wikipedia.org/wiki/Proposition).
If required, Motiv can generate human-readable explanations of the results, which can be presented to 
users or 
leveraged to gain valuable insights into the decision-making process.

The following is a minimalist example of a proposition
```csharp
Spec.Build((int n) => n == 0).Create("empty");
```

Propositions can then be easily composed into new propositions without losing any context.

```csharp
// Define clauses/propositions
var isValid = Spec.Build((int n) => n is >= 0 and <= 11).Create("valid");
var isEmpty = Spec.Build((int n) => n == 0).Create("empty");
var isFull = Spec.Build((int n) => n == 11).Create("full");

// Compose new proposition
var isPartiallyFull = isValid & !(isEmpty | isFull);

// Evaluate proposition
var result = isPartiallyFull.IsSatisfiedBy(5);

result.Satisfied;   // true
result.Assertions;  // ["valid", "!empty", "!full"]
```

You also have a lot more options at your disposal allowing you to craft results that suit your very specific needs.

## Installation

Motiv is available as a [Nuget Package](https://www.nuget.org/packages/Motiv/)
that can be installed via NuGet Package Manager Console by running the following command:

```bash
Install-Package Motiv
```
or by using the .NET CLI:
```bash
dotnet add package Motiv
```

## Usage

### Create a proposition

Think of propositions as the fundamental building blocks of Motiv that can later be reused and composed to form new 
propositions.
Creating them is a fluid process, with a variety of methods available to cater to your specific needs.

#### Minimal

To create a proposition with the minimal of fuss, you can omit the `WhenTrue()` and `WhenFalse()` methods.
In this case, Motiv will automatically use the propositional statement when the proposition is satisfied, and prefix 
it with a "!" when it is not. 

For example, the following proposition will return "is even" when the number is even and "!is even" when it is not.

```csharp
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .Create("is even");

isEven.IsSatisfiedBy(2).Satisfied;   // true
isEven.IsSatisfiedBy(2).Reason;      // "is even"
isEven.IsSatisfiedBy(2).Assertions;  // ["is even"]

isEven.IsSatisfiedBy(3).Satisfied;   // false
isEven.IsSatisfiedBy(3).Reason;      // "!is even"
isEven.IsSatisfiedBy(3).Assertions;  // ["!is even"]
```

#### Explicit Assertions

If you want a better explanation, you can use the `WhenTrue()` and `WhenFalse()` methods to explicitly define 
the assertions that should be used when the proposition is satisfied or not

```csharp
// define a proposition
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("is even")
        .WhenFalse("is odd")
        .Create();

isEven.IsSatisfiedBy(2).Satisfied;  // true
isEven.IsSatisfiedBy(2).Reason;     // "is even"
isEven.IsSatisfiedBy(2).Assertions; // ["is even"]

isEven.IsSatisfiedBy(3).Satisfied;  // false
isEven.IsSatisfiedBy(3).Reason;     // "is odd"
isEven.IsSatisfiedBy(3).Assertions; // ["is odd"]
```

#### Explicit Metadata

Sometimes simple assertions are not enough, and you need to handle more context about the results.
This is what we refer to as _metadata_.
So instead of providing a string, you can provide a POCO object that will be treated in the same way.
The only difference is that metadata will instead populate the `Metadata` property of the result.

```csharp
// define a proposition
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue(new MyMetadata("is even"))
        .WhenFalse(new MyMetadata("is odd"))
        .Create("is even");

// evaluate a model against the proposition
var result = isEven.IsSatisfiedBy(2);

result.Satisfied;  // true
result.Reason;     // "is even"
result.Assertions; // ["is even"]
result.Metadata;   // [{ Text: "is even" }]
```

These examples serve as an introduction to the fundamental building blocks of Motiv, and should not be considered as 
an alternative approach to the humble if-statement.

### Composing propositions

Motiv's strengths really start to show as we scale up.

The following is an example of solving the famous [Fizz Buzz](https://en.wikipedia.org/wiki/Fizz_buzz) challenge 
using Motiv.
If you are unfamiliar, numbers that are multiples of 3 are replaced with "fizz", numbers that are multiples of 5
are replaced with "buzz", and numbers that are multiples of both 3 and 5 are replaced with "fizzbuzz".
If none of these conditions are met, the number is returned as a string.

```csharp
var isFizz = 
    Spec.Build((int n) => n % 3 == 0)
        .Create("fizz");

var isBuzz =
    Spec.Build((int n) => n % 5 == 0)
        .Create("buzz");

var isSubstitution = 
    Spec.Build(isFizz | isBuzz)
        .WhenTrue((_, result) => string.Concat(result.Assertions))
        .WhenFalse(n => n.ToString())
        .Create("should substitute number");

isSubstitution.IsSatisfiedBy(2).Satisfied;   // false
isSubstitution.IsSatisfiedBy(2).Reason;      // "2"

isSubstitution.IsSatisfiedBy(3).Satisfied;   // true
isSubstitution.IsSatisfiedBy(3).Reason;      // "fizz"

isSubstitution.IsSatisfiedBy(4).Satisfied;   // false
isSubstitution.IsSatisfiedBy(4).Reason;      // "4"

isSubstitution.IsSatisfiedBy(5).Satisfied;   // true
isSubstitution.IsSatisfiedBy(5).Reason;      // "buzz"

isSubstitution.IsSatisfiedBy(15).Satisfied;  // true
isSubstitution.IsSatisfiedBy(15).Reason;     // "fizzbuzz"
```

While there might be more optimal solutions to Fizz Buzz out there, this example demonstrates how effortlessly you 
can compose complex propositions from simpler ones using Motiv. 

## Should I use Motiv?

Motiv is not designed to completely replace regular boolean logic.
You should only use it when it makes sense to do so.
If your logic is pretty straightforward or does not really need any feedback about the decisions being made, then you 
might not see a big benefit from using Motiv.
It is just another tool in your toolbox, and sometimes the simplest solution is the best fit.

But when your needs are a bit more complex, that is where Motiv really shines.
Its value will likely become clear if you are looking to achieve two or more of the following goals: 

1. **Visibility**: Provide granular, real-time feedback about decisions made.
2. **Decomposition**: Break down complex or deeply nested logic into meaningful subclauses for better readability.
3. **Reusability**: Reuse your logic in multiple places to avoid duplication.
4. **Modeling**: Explicitly model your domain logic (e.g.
   [Domain Driven Design](https://en.wikipedia.org/wiki/Domain-driven_design)).
5. **Testing**: Easily and thoroughly test your logic (especially by avoiding the need to mock or stub out 
   dependencies).

### Tradeoffs

1. **Performance**: Motiv is not designed for high-performance scenarios where every nanosecond counts.
   Its focus is on maintainability and readability.
   That being said, for most use cases, the performance overhead is negligible. 
2. **Dependency**: Once embedded in your codebase, removing Motiv can be challenging.
   However, it does not depend on any third-party libraries itself, so it won't bring any unexpected baggage. 
3. **Learning Curve**: If you are new to Motiv, you might take a moment to familiarize yourself with its approach,
   but the library has been carefully designed to be as intuitive as possible, with the aim being that developers can 
   quickly understand its concepts and start using it effectively from the get-go.

<div style="display: flex; justify-content: right;">
    <a href="./docs/Spec.html">Next &gt;</a>
</div>