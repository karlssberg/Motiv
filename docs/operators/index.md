---
title: Logical Operators
description: Documentation for logical operators in Motiv that allow combining propositions.
---

# Logical Operators

Motiv provides a set of logical operators to combine [propositions](xref:Motiv.SpecBase`2) and their [results](xref:Motiv.BooleanResultBase`1), enabling the construction of more complex expressions. These operators are applicable to both propositions themselves and the outcomes of their evaluations.

## Available Operators

| Operation                            | Method Usage | Operator Usage                                                                        | Description                                                                                                                               |
|:------------------------------------|:-----------------------------------------------------------|-----------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| [And()](./operators/And.md)         | `left.And(right)` |`left & right`                                                                               | Performs a logical AND operation on two propositions or their results.                                                                  |
| [AndAlso()](./operators/AndAlso.md) | `left.AndAlso(right)` |`left && right`<br>([results](xref:Motiv.BooleanResultBase`1) and expression trees only)                       | Performs a logical AND operation with short-circuiting behavior. The `&&` operator overload is available only for proposition results and within expression trees.     |
| [Or()](./operators/Or.md)           | `left.Or(right)`  |`left \| right`                                                          | Performs a logical OR operation on two propositions or their results.                                                                   |
| [OrElse()](./operators/OrElse.md)   | `left.OrElse(right)` |`left \|\| right`<br>([results](xref:Motiv.BooleanResultBase`1) and expression trees only) | Performs a logical OR operation with short-circuiting behavior. The `\|\|` operator overload is available only for proposition results and within expression trees.     |
| [XOr()](./operators/XOr.md)         | `left.XOr(right)`|`left ^ right`                                                                              | Performs a logical XOR (exclusive OR) operation on two propositions or their results.                                                 |
| [Not()](./operators/Not.md)         | `proposition.Not()`|`!proposition`                                                                            | Performs a logical NOT (negation) operation on a proposition or its result.                                                             |

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
var adultResult = isAdult.IsSatisfiedBy(person);
var idResult = hasValidId.IsSatisfiedBy(person);

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
