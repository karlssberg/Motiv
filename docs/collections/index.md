---
title: Collections API
description: Documentation for working with collections of specifications and results in Motiv
---

# Collections API

Motiv provides extension methods to work efficiently with collections of specifications and results. These methods improve code readability and reduce the need for repetitive logic when dealing with multiple specifications or results.

## Collection Categories

The Collections API is divided into three main categories:

1. **Generic Collections** - Methods for working with collections of any type using specifications
2. **Proposition Collections** - Methods for combining multiple specifications into a single specification
3. **Result Collections** - Methods for working with collections of evaluation results

## Generic Collection Methods

These methods help you work with generic collections using specifications:

| Method | Description |
|--------|-------------|
| [Where&lt;T&gt;()](generic/Where.md) | Filters a collection using a specification instead of a predicate function |

## Proposition Collection Methods

These methods help you combine multiple specifications:

| Method | Description |
|--------|-------------|
| [AndTogether()](propositions/AndTogether.md) | Combines multiple specifications with AND logic |
| [AndAlsoTogether()](propositions/AndAlsoTogether.md) | Combines multiple specifications with short-circuiting AND logic |
| [OrTogether()](propositions/OrTogether.md) | Combines multiple specifications with OR logic |
| [OrElseTogether()](propositions/OrElseTogether.md) | Combines multiple specifications with short-circuiting OR logic |

## Result Collection Methods

These methods help you work with collections of evaluation results:

| Method | Description |
|--------|-------------|
| [WhereTrue()](results/WhereTrue.md) | Filters results to include only satisfied ones |
| [WhereFalse()](results/WhereFalse.md) | Filters results to include only unsatisfied ones |
| [CountTrue()](results/CountTrue.md) | Counts the number of satisfied results |
| [CountFalse()](results/CountFalse.md) | Counts the number of unsatisfied results |
| [AllTrue()](results/AllTrue.md) | Determines if all results are satisfied |
| [AllFalse()](results/AllFalse.md) | Determines if all results are unsatisfied |
| [AnyTrue()](results/AnyTrue.md) | Determines if any results are satisfied |
| [AnyFalse()](results/AnyFalse.md) | Determines if any results are unsatisfied |
| [GetAssertions()](results/GetAssertions.md) | Aggregates assertions from all results |
| [GetTrueAssertions()](results/GetTrueAssertions.md) | Aggregates assertions from satisfied results |
| [GetFalseAssertions()](results/GetFalseAssertions.md) | Aggregates assertions from unsatisfied results |
| [GetRootAssertions()](results/GetRootAssertions.md) | Aggregates assertions from root cause results |
| [GetAllRootAssertions()](results/GetAllRootAssertions.md) | Aggregates assertions from all contributing results |

## Examples

### Working with Generic Collections

```csharp
// Create a specification for adults
var isAdult = Spec
    .Build<Person>(p => p.Age >= 18)
    .Create();

// Use it to filter a collection (instead of people.Where(p => p.Age >= 18))
var adults = people.Where(isAdult);
```

### Combining Specifications

```csharp
// Create multiple specifications
var specs = new List<Spec<Product>>
{
    isInStock,
    isPriceValid,
    hasValidCategory
};

// Combine them all with AND logic
var allChecks = specs.AndTogether();

// Evaluate a product against all checks at once
var result = allChecks.IsSatisfiedBy(product);
```

### Working with Result Collections

```csharp
// Evaluate multiple entities
var results = products.Select(p => productSpec.IsSatisfiedBy(p)).ToList();

// Get only the valid ones
var validResults = results.WhereTrue();

// Check if any are invalid
bool hasInvalid = results.AnyFalse();

// Get all failure messages
string allFailures = string.Join(", ", results.GetFalseAssertions());
```

## Next Steps

- Learn about creating specifications with the [Builder API](../builder/index.md)
- See how to combine specifications with [Logical Operators](../operators/index.md)
- Review the [Getting Started](../getting-started.md) guide for more examples
