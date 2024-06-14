﻿---
title: API
---
# Creation

New propositions are created fluently by initially calling overloads of the `Spec.Build()` method.

| Method                                               | Description                                                                                                           |
|------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------|
| [Build()](./Build.html)                              | Initiates the build process based on a predicate or an existing proposition                                           |
| [As()](./As.html)                                    | (Optional) Defines a custom higher order proposition                                                                  |
| [AsAllSatisfied()](./As.html#all-satisfied)          | (Optional) Defines a proposition that is satisfied when all <br/> odels in the collection are satisfied               |
| [AsAnySatisfied()](./As.html#some-satisfied)         | (Optional) Defines a proposition that is satisfied when any <br/> of the models in the collection are satisfied       |
| [AsNoneSatisfied()](./As.html#none-satisfied)        | (Optional) Defines a proposition that is satisfied when none <br/> of the models in the collection are satisfied      |
| [AsAtLeastNSatisfied()](./As.html#minimum-satisfied) | (Optional) Defines a proposition that is satisfied when at least `n` <br/> models in the collection are satisfied     |
| [AsAtMostNSatisfied()](./As.html#maximum-satisfied)  | (Optional) Defines a proposition that is satisfied when no more than `n` <br/> models in the collection are satisfied |
| [AsNSatisfied()](./As.html#n-satisfied)              | (Optional) Defines a proposition that is satisfied when `n` <br/> number of models in the collection are satisfied    |
| [WhenTrue()](./WhenTrue.html)                        | (Optional) Defines the assertion when the proposition is satisfied                                                    |
| [WhenTrueYield()](./WhenTrueYield.html)              | (Optional) Defines a collection of assertions to return when                                                          |
| [WhenFalse()](./WhenFalse.html)                      | (Optional) Defines the assertion when the proposition is not satisfied                                                |
| [WhenFalseYield()](./WhenFalseYield.html)            | (Optional) Defines a collection of assertions to return when the proposition is not satisfied                         |
| [Create()](./Create.html)                            | Completes the build process and returns the proposition                                                               |

# Operations

Propositions can be operated on to form new propositions.

| Operator                    | Syntax                | Description           |
|:----------------------------|-----------------------|-----------------------|
| [And()](./And.html)         | `left & right`        | Performs a logical AND upon two propositions |
| [AndAlso()](./AndAlso.html) | `left.AndAlso(right)` | Performs a short-circuiting logical AND upon two propositions |
| [Or()](./Or.html)           | `left \| right`       | Performs a logical OR upon two propositions |
| [OrElse()](./OrElse.html)   | `left.OrElse(right)`  | Performs a short-circuiting logical OR upon two propositions |
| [XOr()](./XOr.html)         | `left ^ right`        | Performs a logical XOR upon two propositions |
| [Not()](./Not.html)         | `!proposition`        | Performs a logical NOT upon a proposition |