---
title: Operators
category: operators
---

Propositions can be composed together to form new propositions using the following operations

| Operator                     | Syntax                | Description           |
|:-----------------------------|-----------------------|-----------------------|
| [And()](/And.html)           | `left & right`        | Performs a logical AND upon two propositions |
| [AndAlso()](/AndAlso.html)   | `left.AndAlso(right)` | Performs a short-circuiting logical AND upon two propositions |
| [Or()](/Or.html)             | `left \| right`       | Performs a logical OR upon two propositions |
| [OrElse()](/OrElse.html)     | `left.OrElse(right)`  | Performs a short-circuiting logical OR upon two propositions |
| [XOr()](/XOr.html)           | `left ^ right`        | Performs a logical XOR upon two propositions |
| [Not()](/Not.html)           | `!proposition`        | Performs a logical NOT upon a proposition |