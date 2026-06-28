---
title: Builder API
description: Documentation for the fluent builder API in Motiv
---
The Builder API provides a fluent interface for creating specifications and propositions in Motiv. This allows you to construct complex expressions in a readable, maintainable way.

## Core Builder Methods

Building specifications in Motiv follows a consistent pattern:

1. Start with `Spec.Build()` to begin the specification
2. Optionally add higher-order proposition methods like `AsAllSatisfied()`
3. Define outcomes with `WhenTrue()` and `WhenFalse()`
4. Complete with `Create()` to produce the final specification

## Available Builder Methods

| Method | Description |
|--------|-------------|
| [Build()](Build.md) | Initiates the build process |
| [As()](As.md) | Defines a custom higher order proposition |
| [AsAllSatisfied()](AsAllSatisfied.md) | Creates proposition satisfied when all models in a collection are satisfied |
| [AsAnySatisfied()](AsAnySatisfied.md) | Creates proposition satisfied when any model in a collection is satisfied |
| [AsNoneSatisfied()](AsNoneSatisfied.md) | Creates proposition satisfied when no models in a collection are satisfied |
| [AsAtLeastNSatisfied()](AsAtLeastNSatisfied.md) | Creates proposition satisfied when at least N models are satisfied |
| [AsAtMostNSatisfied()](AsAtMostNSatisfied.md) | Creates proposition satisfied when at most N models are satisfied |
| [AsNSatisfied()](AsNSatisfied.md) | Creates proposition satisfied when exactly N models are satisfied |
| [WhenTrue()](WhenTrue.md) | Defines the explanation when the proposition is satisfied |
| [WhenTrueYield()](WhenTrueYield.md) | Defines a collection of assertions when satisfied |
| [WhenFalse()](WhenFalse.md) | Defines the explanation when the proposition is not satisfied |
| [WhenFalseYield()](WhenFalseYield.md) | Defines a collection of assertions when not satisfied |
| [Create()](Create.md) | Completes the build process and returns the proposition |

## Example

Here's a simple example of the builder in action:

```csharp
// Create a specification that validates if a person is allowed to drive
var canDrive = Spec
    .Build<Person>(person => person.Age >= 16)
    .WhenTrue("Person is old enough to drive")
    .WhenFalse("Person is too young to drive")
    .Create();

// For collection-based specifications:
var allAdults = Spec
    .Build<IEnumerable<Person>>()
    .AsAllSatisfied(person => person.Age >= 18)
    .WhenTrue("All persons are adults")
    .WhenFalse("Some persons are minors")
    .Create();
```

## Next Steps

After creating specifications, you'll likely want to:

- [Combine specifications](../operators/index.md) using logical operators
- Work with [collections of specifications](../collections/index.md)
- [Evaluate specifications](../getting-started.md#creating-your-first-specification) against your domain models
