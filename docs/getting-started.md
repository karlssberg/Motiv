---
title: Getting Started with Motiv
description: Build expressive, composable logical propositions with Motiv - a .NET library that solves the Boolean Blindness problem
---
## What is Motiv?

Motiv is a .NET library that solves the
[_Boolean Blindness Problem_](https://existentialtype.wordpress.com/2011/03/15/boolean-blindness/)
by transforming simple boolean expressions into rich, self-documenting specifications.

When you evaluate a traditional boolean expression,
you only get `true` or `false` &mdash; you lose the context of *why* that result occurred.
Motiv preserves this critical information by structuring boolean logic as composable specifications that retain
their reasoning and can be combined to express complex logical propositions.

## Installation

Install via the .NET CLI:

```bash
dotnet add package Motiv
```

Or via the NuGet Package Manager:

```
Install-Package Motiv
```

## Core Concepts

Motiv is built around three fundamental concepts that work together to solve the Boolean Blindness problem:

| Concept            | Description                                                                                       |
|--------------------|---------------------------------------------------------------------------------------------------|
| **Specifications** | The building blocks of logical propositions. Encapsulate boolean expressions or operations          |
| **Propositions**   | One or more specifications combined to form a meaningful logical statement                        |
| **Results**        | Rich objects that include both the boolean outcome and detailed metadata about why/how           |

## Creating Your First Specification

Here's how to create a basic age verification specification:

```csharp
using Motiv;

// Define a model
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

// Create a specification with custom messages
var isAdult = Spec
    .Build((Person person) => person.Age >= 18)
    .WhenTrue("is an adult")
    .WhenFalse("is underage")
    .Create();

// Apply the specification to a model
var person = new Person { Name = "Benjamin", Age = 25 };
var result = isAdult.IsSatisfiedBy(person);

Console.WriteLine(result.Satisfied);    // true
Console.WriteLine(result.Explanation);  // "is an adult"
```

### Developer-Friendly Specifications with `Spec.From()`

For development and debugging scenarios, `Spec.From()` generates descriptive technical assertions:

```csharp
// Create a specification with auto-generated assertions
var isAdultFrom = Spec
    .From((Person person) => person.Age >= 18)
    .Create("is adult"); // This name is used in explanations

var person = new Person { Name = "Alice", Age = 16 };
var result = isAdultFrom.IsSatisfiedBy(person);

// Technical output for debugging
if (!result.Satisfied)
{
    Console.WriteLine(string.Join(", ", result.Assertions)); // "person.Age < 18"
}
```

The `Spec.From()` approach is particularly useful during development and unit testing when you need precise details
about why a specification failed.

## Composing Specifications

One of Motiv's most powerful features is the ability to combine specifications using logical operators,
allowing you to build complex logical propositions from simple parts:

```csharp
// Create individual specifications
var isAdult = Spec
    .Build<Person>(p => p.Age >= 18)
    .WhenTrue("is an adult")
    .WhenFalse("is underage")
    .Create();

var hasValidName = Spec
    .Build<Person>(p => !string.IsNullOrWhiteSpace(p.Name))
    .WhenTrue("Name is provided")
    .WhenFalse("Name is missing")
    .Create();

// Combine specifications using logical operators
var isValidPerson = isAdult.And(hasValidName);

// Alternative operator syntax
// var isValidPerson = isAdult & hasValidName;

// Apply the combined specification
var person = new Person { Name = "", Age = 15 };
var result = isValidPerson.IsSatisfiedBy(person);

// Result contains all failed conditions
Console.WriteLine(result.Satisfied);    // false
Console.WriteLine(result.Explanations); // ["is underage", "Name is missing"]
```

## Complex Logical Scenarios

Motiv can express complex logical relationships through composition:

```csharp
public class Product
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool IsOnSale { get; set; }
    public decimal SaleDiscount { get; set; }
}

// Define individual specifications
var hasValidName = Spec
    .Build((Product p) => !string.IsNullOrWhiteSpace(p.Name))
    .WhenTrue("Product name is present")
    .WhenFalse("Product name is required")
    .Create();

var hasPositivePrice = Spec
    .Build((Product p) => p.Price > 0)
    .WhenTrue("Price is greater than zero")
    .WhenFalse("Price must be greater than zero")
    .Create();

var hasValidDiscount = Spec
    .Build((Product p) => !p.IsOnSale || (p.SaleDiscount > 0 && p.SaleDiscount < p.Price))
    .WhenTrue("Product has a valid discount")
    .WhenFalse("Product must have a valid discount")
    .Create();

// Combine all specifications
var isValidProduct = hasValidName & hasPositivePrice & hasValidDiscount;
```

## Best Practices

1. **Name propositions meaningfully** &ndash; Clear names make code self-documenting
2. **Keep propositions focused** &ndash; Each proposition should express one logical concept
3. **Compose small propositions** &ndash; Build complex logic from simple building blocks
4. **Use custom messages** &ndash; Provide clear, context-specific explanations
5. **Consider internationalization** &ndash; For user-facing messages, use resource files

## Next Steps

- Explore the [Builder API](./builder/index.md) for creating expressive propositions
- Learn about [Logical Operators](./operators/index.md) to combine propositions
- See how to work with [Collections](./collections/index.md) of propositions and results
- Understand [Performance considerations](../performance/index.md)
- [API documentation](../../api/index.md)
