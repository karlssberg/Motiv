---
title: API
---
# Creation

New propositions are created fluently by initially calling overloads of the `Spec.Build()` method.

| Method                                               | Description                                                                                                      |
|------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| [Build()](./Build.html)                              | Initiates the build process based on a predicate or an existing proposition or one of its results.               |
| [As()](./As.html)                                    | (Optional) Defines a custom higher order proposition.                                                            |
| [AsAllSatisfied()](./As.html#all-satisfied)          | (Optional) Defines a proposition that is satisfied when all odels in the collection are satisfied.               |
| [AsAnySatisfied()](./As.html#some-satisfied)         | (Optional) Defines a proposition that is satisfied when any of the models in the collection are satisfied.       |
| [AsNoneSatisfied()](./As.html#none-satisfied)        | (Optional) Defines a proposition that is satisfied when none of the models in the collection are satisfied.      |
| [AsAtLeastNSatisfied()](./As.html#minimum-satisfied) | (Optional) Defines a proposition that is satisfied when at least `n` models in the collection are satisfied.     |
| [AsAtMostNSatisfied()](./As.html#maximum-satisfied)  | (Optional) Defines a proposition that is satisfied when no more than `n` models in the collection are satisfied. |
| [AsNSatisfied()](./As.html#n-satisfied)              | (Optional) Defines a proposition that is satisfied when `n` number of models in the collection are satisfied.    |
| [WhenTrue()](./WhenTrue.html)                        | (Optional) Defines the assertion when the proposition is satisfied.                                              |
| [WhenTrueYield()](./WhenTrueYield.html)              | (Optional) Defines a collection of assertions to return when satisfied.                                          |
| [WhenFalse()](./WhenFalse.html)                      | (Optional) Defines the assertion when the proposition is not satisfied.                                          |
| [WhenFalseYield()](./WhenFalseYield.html)            | (Optional) Defines a collection of assertions to return when the proposition is not satisfied.                   |
| [Create()](./Create.html)                            | Completes the build process and returns the proposition.                                                         |

# Boolean Operations

Propositions can be logically operated on, and in doing so they form new propositions.
You can also use boolean operators to combine the results (`BooleanResultBase`) return by propositions.
This is useful when you have two propositions that work with completely different models, but they need to be logically 
combined to give a single result.

| Operator                    | Syntax                                                              | Description                                                                |
|:----------------------------|:--------------------------------------------------------------------|----------------------------------------------------------------------------|
| [And()](./And.html)         | `left.And(right)` <br/>or<br/>`left & right`                        | Performs a logical AND <br /> on two propositions/results.                 |
| [AndAlso()](./AndAlso.html) | `left.AndAlso(right)` <br />or<br />`left && right` (results only)  | Performs a short-circuiting logical AND <br />on two propositions/results. |
| [Or()](./Or.html)           | `left.Or(right)`  <br />or<br />`left                               | right`                                                            | Performs a logical OR <br /> on two propositions/results.                    |
| [OrElse()](./OrElse.html)   | `left.OrElse(right)`  <br />or<br />`left                           || right` (results only)                                               | Performs a short-circuiting logical OR <br /> on two propositions/results.  |
| [XOr()](./XOr.html)         | `left.XOr(right)`<br />or<br />`left ^ right`                       | Performs a logical XOR <br /> on two propositions/results.                 |
| [Not()](./Not.html)         | `proposition.Not()`<br />or<br />`!proposition`                      | Performs a logical NOT <br /> on a proposition/result.                     |

# Collections Extension Methods

Motiv provides a set of extension methods to improve the readability of your code when working with collections of 
propositions or their results.

| Method                                                            | Description                                                                                                                                                                                               |
|-------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [Where&lt;T&gt;()](./Collections.html#where)                      | Filters an `IEnumerable<T>` using a proposition instead of a predicate function.                                                                                                                          |
| [WhereTrue()](./Collections.html#wheretrue)                       | Filters a collection of `BooleanResultBase<TMetadata>` so that only satisfied remain.                                                                                                                     |
| [WhereFalse()](./Collections.html#wherefalse)                     | Filters a collection of `BooleanResultBase<TMetadata>` so that only unsatisfied remain.                                                                                                                   |
| [CountTrue()](./Collections.html#counttrue)                       | Counts the number of satisfied `BooleanResultBase<TMetadata>` in a collection.                                                                                                                            |
| [CountFalse()](./Collections.html#countfalse)                     | Counts the number of unsatisfied `BooleanResultBase<TMetadata>` in a collection.                                                                                                                          |
| [AllTrue()](./Collections.html#alltrue)                           | Determines whether all the BooleanResultBase<TMetadata>` in a collection are satisfied.                                                                                                                   |
| [AllFalse()](./Collections.html#allfalse)                         | Determines whether all the `BooleanResultBase<TMetadata>` in a collection are unsatisfied.                                                                                                                |
| [AnyTrue()](./Collections.html#anytrue)                           | Determines whether any `BooleanResultBase<TMetadata>` in a collection are satisfied.                                                                                                                      |
| [AnyFalse()](./Collections.html#anyfalse)                         | Determines whether any `BooleanResultBase<TMetadata>` in a collection are unsatisfied.                                                                                                                    |
| [GetAssertions()](./Collections.html#getassertions)               | Aggregates the assertions from a collection of `BooleanResultBase<TMetadata>`.                                                                                                                            |
| [GetTrueAssertions()](./Collections.html#gettrueassertions)       | Aggregates the assertions from a collection of `BooleanResultBase<TMetadata>` filtered to only include satisfied results.                                                                                 |
| [GetFalseAssertions()](./Collections.html#getfalseassertions)     | Aggregates the assertions from a collection of `BooleanResultBase<TMetadata>` filtered to only include unsatisfied results.                                                                               |
| [GetRootAssertions()](./Collections.html#getrootassertions)       | Finds the `BooleanResultBase<TMetadata>` that are the root causes and aggregates their assertions.                                                                                                        |
| [GetAllRootAssertions()](./Collections.html#getallrootassertions) | Finds all the `BooleanResultBase<TMetadata>` involved and aggregates their assertions (regardless of whether they helped determine the final results).                                                    |
| [AndTogether()](./Collections.html#andtogether)                   | Creates a new proposition that is the logical [And()](./And.html) of the propositions in a collection. This also applies to [boolean results](./Collections.html#collections-of-boolean-results).         |
| [AndAlsoTogether()](./Collections.html#andalsotogether)           | Creates a new proposition that is the logical [AndAlso()](./AndAlso.html) of the propositions in a collection. This also applies to [boolean results](./Collections.html#collections-of-boolean-results). |
| [OrTogether()](./Collections.html#ortogether)                     | Creates a new proposition that is the logical [Or()](./Or.html) of the propositions in a collection. This also applies to [boolean results](./Collections.html#collections-of-boolean-results).           |
| [OrElseTogether()](./Collections.html#orelsetogether)             | Creates a new proposition that is the logical [OrElse()](./OrElse.html) of the propositions in a collection. This also applies to [boolean results](./Collections.html#collections-of-boolean-results).   |
