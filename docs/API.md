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

Propositions can be operated on to form new propositions.
You can also use boolean operators to combine the results (`BooleanResultBase`) return by propositions.
This is useful when you have two propositions that work with completely different models, but they need to be logically 
combined to give a single result.

| Operator                    | Syntax                                                                | Description                                                                |
|:----------------------------|:----------------------------------------------------------------------|----------------------------------------------------------------------------|
| [And()](./And.html)         | `left.And(right)` <br/>or<br/> `left & right`                         | Performs a logical AND <br /> on two propositions/results.                 |
| [AndAlso()](./AndAlso.html) | `left.AndAlso(right)` <br /> or <br /> `left && right` (results only) | Performs a short-circuiting logical AND <br />on two propositions/results. |
| [Or()](./Or.html)           | `left.Or(right)`  <br /> or <br /> `left                              | right`                                                            | Performs a logical OR <br /> on two propositions/results.                    |
| [OrElse()](./OrElse.html)   | `left.OrElse(right)`  <br /> or <br /> `left                          || right` (results only)                                               | Performs a short-circuiting logical OR <br /> on two propositions/results.  |
| [XOr()](./XOr.html)         | `left ^ right`                                                        | Performs a logical XOR <br /> on two propositions/results.                 |
| [Not()](./Not.html)         | `!proposition`                                                        | Performs a logical NOT <br /> on a proposition/result.                     |
