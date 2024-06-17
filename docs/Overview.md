---
title: API
---
# Overview

## Builder

New [propositions](xref:Motiv.SpecBase`2) are created fluently by initially calling overloads of the
[`Spec.Build()`](./builder/Build.md) method.

| Method                                                   | Description                                                                                                      |
|----------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| [Build()](./builder/Build.md)                            | Initiates the build process based on a predicate or an existing proposition or one of its results.               |
| [As()](./builder/As.md)                                  | (Optional) Defines a custom higher order proposition.                                                            |
| [AsAllSatisfied()](./builder/AsAllSatisfied.md)          | (Optional) Defines a proposition that is satisfied when all odels in the collection are satisfied.               |
| [AsAnySatisfied()](./builder/AsAnySatisfied.md)          | (Optional) Defines a proposition that is satisfied when any of the models in the collection are satisfied.       |
| [AsNoneSatisfied()](./builder/AsNoneSatisfied.md)        | (Optional) Defines a proposition that is satisfied when none of the models in the collection are satisfied.      |
| [AsAtLeastNSatisfied()](./builder/AsAtLeastNSatisfied.md) | (Optional) Defines a proposition that is satisfied when at least `n` models in the collection are satisfied.     |
| [AsAtMostNSatisfied()](./builder/AsAtMostNSatisfied.md)  | (Optional) Defines a proposition that is satisfied when no more than `n` models in the collection are satisfied. |
| [AsNSatisfied()](./builder/AsNSatisfied.md)              | (Optional) Defines a proposition that is satisfied when `n` number of models in the collection are satisfied.    |
| [WhenTrue()](./builder/WhenTrue.md)                    | (Optional) Defines the assertion when the proposition is satisfied.                                              |
| [WhenTrueYield()](./builder/WhenTrueYield.md)            | (Optional) Defines a collection of assertions to return when satisfied.                                          |
| [WhenFalse()](./builder/WhenFalse.md)                    | (Optional) Defines the assertion when the proposition is not satisfied.                                          |
| [WhenFalseYield()](./builder/WhenFalseYield.md)          | (Optional) Defines a collection of assertions to return when the proposition is not satisfied.                   |
| [Create()](./builder/Create.md)                          | Completes the build process and returns the proposition.                                                         |

## Operators

Propositions can be logically operated on, and in doing so they form new propositions.
You can also use boolean operators to combine the [results](xref:Motiv.BooleanResultBase`1) return by propositions.
This is useful when you have two propositions that work with completely different models, but they need to be logically 
combined to give a single result.

| Operator                            | Syntax                                                                                                                     | Description                                                                |
|:------------------------------------|:---------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------| 
| [And()](./operators/And.md)         | `left.And(right)` <br/>or<br/>`left & right`                                                                               | Performs a logical AND on two propositions/results.                 |
| [AndAlso()](./operators/AndAlso.md) | `left.AndAlso(right)` <br />or<br />`left && right` ([results](xref:Motiv.BooleanResultBase`1) only)                       | Performs a short-circuiting logical ANDon two propositions/results. | 
| [Or()](./operators/Or.md)           | `left.Or(right)`  <br />or<br /> <code>left &#124; right</code>                                                            | Performs a logical OR on two propositions/results.                  |
| [OrElse()](./operators/OrElse.md)   | `left.OrElse(right)`  <br />or<br /><code>left &#124;&#124; right</code>  ([results](xref:Motiv.BooleanResultBase`1) only) | Performs a short-circuiting logical OR on two propositions/results. |
| [XOr()](./operators/XOr.md)         | `left.XOr(right)`<br />or<br />`left ^ right`                                                                              | Performs a logical XOR on two propositions/results.                 |
| [Not()](./operators/Not.md)         | `proposition.Not()`<br />or<br />`!proposition`                                                                            | Performs a logical NOT on a proposition/result.                     |

## Collections

Motiv provides a set of extension methods to improve the readability of your code when working with collections of 
propositions or their results.

| Method                                                               | Description                                                                                                                                                                                                   |
|----------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [Where&lt;T&gt;()](./collections/generic/Where.md)                   | Filters an collection using a proposition instead of a predicate function.                                                                                                                                    |
| [WhereTrue()](./collections/results/WhereTrue.md)                    | Filters a collection of boolean results so that only satisfied remain.                                                                                                                                        |
| [WhereFalse()](./collections/results/WhereFalse.md)                  | Filters a collection of boolean results so that only unsatisfied remain.                                                                                                                                      |
| [CountTrue()](./collections/results/CountTrue.md)                    | Counts the number of satisfied boolean results in a collection.                                                                                                                                               |
| [CountFalse()](./collections/results/CountFalse.md)                     | Counts the number of unsatisfied boolean results in a collection.                                                                                                                                             |
| [AllTrue()](./collections/results/AllTrue.md)                           | Determines whether all the <xref:Motiv.BooleanResultBase`1> in a collection are satisfied.                                                                                                                    |
| [AllFalse()](./collections/results/AllFalse.md)                         | Determines whether all the boolean results in a collection are unsatisfied.                                                                                                                                   |
| [AnyTrue()](./collections/results/AnyTrue.md)                           | Determines whether any boolean results in a collection are satisfied.                                                                                                                                         |
| [AnyFalse()](./collections/results/AnyFalse.md)                         | Determines whether any boolean results in a collection are unsatisfied.                                                                                                                                       |
| [GetAssertions()](./collections/results/GetAssertions.md)               | Aggregates the assertions from a collection of boolean results.                                                                                                                                               |
| [GetTrueAssertions()](./collections/results/GetTrueAssertions.md)       | Aggregates the assertions from a collection of boolean results filtered to only include satisfied results.                                                                                                    |
| [GetFalseAssertions()](./collections/results/GetFalseAssertions.md)     | Aggregates the assertions from a collection of boolean results filtered to only include unsatisfied results.                                                                                                  |
| [GetRootAssertions()](./collections/results/GetRootAssertions.md)       | Finds the boolean results that are the root causes and aggregates their assertions.                                                                                                                           |
| [GetAllRootAssertions()](./collections/results/GetAllRootAssertions.md) | Finds all the boolean results involved and aggregates their assertions (regardless of whether they helped determine the final results).                                                                       |
| [AndTogether()](./collections/propositions/AndTogether.md)              | Creates a new proposition that is the logical [And()](./operators/And.md) of the propositions in a collection. This also applies to <xref:Motiv.BooleanResultBase`1>.         |
| [AndAlsoTogether()](./collections/propositions/AndAlsoTogether.md)      | Creates a new proposition that is the logical [AndAlso()](./operators/AndAlso.md) of the propositions in a collection. This also applies to <xref:Motiv.BooleanResultBase`1>. |
| [OrTogether()](./collections/propositions/OrTogether.md)                | Creates a new proposition that is the logical [Or()](./operators/Or.md) of the propositions in a collection. This also applies to <xref:Motiv.BooleanResultBase`1>.           |
| [OrElseTogether()](./collections/propositions/OrElseTogether.md)     | Creates a new proposition that is the logical [OrElse()](./operators/OrElse.md) of the propositions in a collection. This also applies to <xref:Motiv.BooleanResultBase`1>.                                   |
