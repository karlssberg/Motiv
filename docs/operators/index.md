---
title: Logical Operators
description: Documentation for logical operators in Motiv that allow combining specifications
---

# Logical Operators

Motiv provides a set of logical operators that allow you to combine specifications and results to build more complex business rules. These operators work with both specifications and their evaluation results.

## Available Operators

| Operator | Method Syntax | Operator Syntax | Description |
|----------|---------------|-----------------|-------------|
| [And](And.md) | `left.And(right)` | `left & right` | Performs a logical AND on two specifications or results |
| [AndAlso](AndAlso.md) | `left.AndAlso(right)` | `left && right` (results only) | Performs a short-circuiting logical AND |
| [Or](Or.md) | `left.Or(right)` | `left \| right` | Performs a logical OR on two specifications or results |
| [OrElse](OrElse.md) | `left.OrElse(right)` | `left \|\| right` (results only) | Performs a short-circuiting logical OR |
| [XOr](XOr.md) | `left.XOr(right)` | `left ^ right` | Performs a logical XOR operation |
| [Not](Not.md) | `proposition.Not()` | `!proposition` | Performs a logical NOT operation |

## Working with Specifications

When combining specifications, the operators create new composite specifications:

```csharp
// Create individual specifications
var isAdult = Spec
    .Build<Person>(p => p.Age >= 18)
    .WhenTrue("Is an adult")
    .WhenFalse("Is not an adult")
    .Create();

var hasValidId = Spec
    .Build<Person>(p => !string.IsNullOrEmpty(p.IdNumber))
    .WhenTrue("Has valid ID")
    .WhenFalse("Missing ID")
    .Create();

// Combine using method syntax
var canVote = isAdult.And(hasValidId);

// Or using operator syntax
var canVote = isAdult & hasValidId;
```

## Working with Results

You can also combine the results of evaluated specifications:

```csharp
var adultResult = isAdult.IsSatisfiedBy(person);
var idResult = hasValidId.IsSatisfiedBy(person);

// Combine results
var canVoteResult = adultResult & idResult;

// Access combined explanations
if (!canVoteResult.Satisfied)
{
    // Will include explanations from both failed specifications
    Console.WriteLine(canVoteResult.Explanation);
}
```

## Operator Differences

- **And** vs **AndAlso**: AndAlso short-circuits evaluation, meaning the right side is only evaluated if the left side is true
- **Or** vs **OrElse**: OrElse short-circuits, meaning the right side is only evaluated if the left side is false

## Next Steps

- Learn more about the [Builder API](../builder/index.md) for creating specifications
- See how to work with [Collections](../collections/index.md) of specifications and results
- Review practical examples in the [Getting Started](../getting-started.md) guide
