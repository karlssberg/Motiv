---
title: Logical Operators
description: Documentation for logical operators in Motiv that allow combining propositions.
---

# Logical Operators

Motiv provides a set of logical operators to combine [propositions](xref:Motiv.SpecBase`2) and their [results](xref:Motiv.BooleanResultBase`1), enabling the construction of more complex expressions. These operators are applicable to both propositions themselves and the outcomes of their evaluations.

## Available Operators

| Operation                            | Method Usage | Operator Usage                                                                        | Description                                                                                                                               |
|:------------------------------------|:-----------------------------------------------------------|-----------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| [And()](operators/And.md)         | `left.And(right)` |`left & right`                                                                               | Creates a new specification that is satisfied if both operand specifications are satisfied. Corresponds to the logical AND operator (`&`).         |
| [AndAlso()](operators/AndAlso.md) | `left.AndAlso(right)` |`left && right`<br>([results](xref:Motiv.BooleanResultBase`1) and expression trees only)                       | Creates a new specification that is satisfied if both operand specifications are satisfied. Corresponds to the logical AND ALSO operator (`&&`).   |
| [Or()](operators/Or.md)           | `left.Or(right)`  |`left \| right`                                                          | Creates a new specification that is satisfied if either of the operand specifications are satisfied. Corresponds to the logical OR operator (`|`).          |
| [OrElse()](operators/OrElse.md)   | `left.OrElse(right)` |`left \|\| right`<br>([results](xref:Motiv.BooleanResultBase`1) and expression trees only) | Creates a new specification that is satisfied if either of the operand specifications are satisfied. Corresponds to the logical OR ELSE operator (`||`).    |
| [XOr()](operators/XOr.md)         | `left.XOr(right)`|`left ^ right`                                                                              | Creates a new specification that is satisfied if exactly one of the operand specifications is satisfied. Corresponds to the logical XOR operator (`^`).      |
| [Not()](operators/Not.md)         | `proposition.Not()`|`!proposition`                                                                            | Creates a new specification that is satisfied if the operand specification is not satisfied. Corresponds to the logical NOT operator (`!`).            |

## Working with Propositions

Combining propositions with these operators generates new, composite propositions:

```csharp
// Create individual propositions
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

// Combine using method syntax to create a new proposition
var canVote = isAdult.And(hasValidId);

// Alternatively, use operator syntax
// var canVote = isAdult & hasValidId;
```

## Working with Results

The results of evaluated propositions can also be combined using these operators:

```csharp
var adultResult = isAdult.Evaluate(person);
var idResult = hasValidId.Evaluate(person);

// Combine the results
var canVoteResult = adultResult & idResult;

// Access the combined explanation
if (!canVoteResult.Satisfied)
{
    // The explanation will include reasons from all failed propositions
    Console.WriteLine(canVoteResult.Explanation.Assertions.First()); // Example: "Is not an adult"
}
```

## Key Operator Differences

- **And** vs. **AndAlso**: `AndAlso` implements short-circuiting. The right-hand operand is evaluated only if the left-hand operand is true. `And` always evaluates both operands.
- **Or** vs. **OrElse**: `OrElse` implements short-circuiting. The right-hand operand is evaluated only if the left-hand operand is false. `Or` always evaluates both operands.

## Next Steps

- Explore the [Builder API](../builder/index.md) for creating propositions.
- Discover how to manage [Collections](../collections/index.md) of propositions and results.
- Consult the [Getting Started](../getting-started.md) guide for practical examples.
