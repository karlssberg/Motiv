---
title: API
---
This document provides an overview of the Motiv API, categorized by functionality.

## Builder

New [propositions](xref:Motiv.SpecBase`2) are created using a fluent interface, starting with an overload of the [`Spec.Build()`](./builder/Build.md) method.

| Method                                                   | Description                                                                                                                              |
|----------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| [Build()](./builder/Build.md)                            | Initiates the proposition construction process. It can be based on a predicate, an existing proposition, or the result of a proposition. |
| [As()](./builder/As.md)                                  | (Optional) Defines a custom higher-order proposition, which is a proposition that operates on or returns other propositions.             |
| [AsAllSatisfied()](./builder/AsAllSatisfied.md)          | (Optional) Defines a proposition that is satisfied if all models in a collection meet the specified criteria.                            |
| [AsAnySatisfied()](./builder/AsAnySatisfied.md)          | (Optional) Defines a proposition that is satisfied if at least one model in a collection meets the specified criteria.                   |
| [AsNoneSatisfied()](./builder/AsNoneSatisfied.md)        | (Optional) Defines a proposition that is satisfied if no models in a collection meet the specified criteria.                             |
| [AsAtLeastNSatisfied()](./builder/AsAtLeastNSatisfied.md) | (Optional) Defines a proposition that is satisfied if at least `n` models in a collection meet the specified criteria.                   |
| [AsAtMostNSatisfied()](./builder/AsAtMostNSatisfied.md)  | (Optional) Defines a proposition that is satisfied if no more than `n` models in a collection meet the specified criteria.               |
| [AsNSatisfied()](./builder/AsNSatisfied.md)              | (Optional) Defines a proposition that is satisfied if exactly `n` models in a collection meet the specified criteria.                    |
| [WhenTrue()](./builder/WhenTrue.md)                    | (Optional) Specifies the value to be used when the proposition is satisfied.                                                             |
| [WhenTrueYield()](./builder/WhenTrueYield.md)            | (Optional) Specifies a collection of values to be returned when the proposition is satisfied.                                            |
| [WhenFalse()](./builder/WhenFalse.md)                    | (Optional) Specifies the value to be used when the proposition is not satisfied.                                                         |
| [WhenFalseYield()](./builder/WhenFalseYield.md)          | (Optional) Specifies a collection of values to be returned when the proposition is not satisfied.                                    |
| [Create()](./builder/Create.md)                          | Finalizes the construction process and returns the configured proposition.                                                               |

## Operators

Propositions can be combined using logical operators to form new, more complex propositions. Boolean operators can also be used to combine the [results](xref:Motiv.BooleanResultBase`1) of propositions. This is particularly useful when you need to logically combine propositions that operate on different model types to produce a single outcome.

| Operation                            | Method Usage                      | Operator Usage                                                                          | Description                                                                                                                               |
|:-----------------------------------|:----------------------------------|:------------------------------------------------------------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------|
| [And()](./operators/And.md)         | `left.And(right)` |`left & right`                                                                               | Performs a logical AND operation on two propositions or their results.                                                                  |
| [AndAlso()](./operators/AndAlso.md) | `left.AndAlso(right)` |`left && right`<br>([results](xref:Motiv.BooleanResultBase`1) and expression trees only)                       | Performs a logical AND operation with short-circuiting behavior. The `&&` operator overload is available only for proposition results and within expression trees.     |
| [Or()](./operators/Or.md)           | `left.Or(right)`  |`left \| right`                                                          | Performs a logical OR operation on two propositions or their results.                                                                   |
| [OrElse()](./operators/OrElse.md)   | `left.OrElse(right)` |`left \|\| right`<br>([results](xref:Motiv.BooleanResultBase`1) and expression trees only) | Performs a logical OR operation with short-circuiting behavior. The `\|\|` operator overload is available only for proposition results and within expression trees.     |
| [XOr()](./operators/XOr.md)         | `left.XOr(right)`|`left ^ right`                                                                              | Performs a logical XOR (exclusive OR) operation on two propositions or their results.                                                 |
| [Not()](./operators/Not.md)         | `proposition.Not()`|`!proposition`                                                                            | Performs a logical NOT (negation) operation on a proposition or its result.                                                             |
| [ChangeModelTo()](./operators/ChangeModelTo.md) | `proposition.ChangeModelTo(selector)` | _(none)_                                                          | Re-points an existing proposition at a different model type, so propositions over differing models can be composed as a single proposition. |

## Tap

Tap extension methods attach side-effects (e.g., logging, metrics) to [propositions](xref:Motiv.SpecBase`2) without altering their logical behavior. The tapped proposition is fully transparent — its result, description, and assertions are identical to the original.

| Method                                  | Description                                                                                                |
|-----------------------------------------|------------------------------------------------------------------------------------------------------------|
| [Tap()](./tap/Tap.md)                   | Fires a callback on every evaluation, regardless of the outcome.                                          |
| [TapWhenTrue()](./tap/TapWhenTrue.md)   | Fires a callback only when the proposition is satisfied.                                                   |
| [TapWhenFalse()](./tap/TapWhenFalse.md) | Fires a callback only when the proposition is not satisfied.                                               |

## Collections

Motiv offers extension methods to enhance code readability when working with collections of propositions or their results.

| Method                                                               | Description                                                                                                                                                              |
|----------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [Where&lt;T&gt;()](./collections/generic/Where.md)                   | Filters a collection using a proposition, similar to LINQ's `Where` but with Motiv's explanatory capabilities.                                                         |
| [WhereTrue()](./collections/results/WhereTrue.md)                    | Filters a collection of boolean results, retaining only those that are satisfied (true).                                                                                 |
| [WhereFalse()](./collections/results/WhereFalse.md)                  | Filters a collection of boolean results, retaining only those that are unsatisfied (false).                                                                              |
| [CountTrue()](./collections/results/CountTrue.md)                    | Counts the number of satisfied (true) boolean results in a collection.                                                                                                   |
| [CountFalse()](./collections/results/CountFalse.md)                     | Counts the number of unsatisfied (false) boolean results in a collection.                                                                                                |
| [AllTrue()](./collections/results/AllTrue.md)                           | Determines if all <xref:Motiv.BooleanResultBase`1> instances in a collection are satisfied (true).                                                                       |
| [AllFalse()](./collections/results/AllFalse.md)                         | Determines if all boolean results in a collection are unsatisfied (false).                                                                                               |
| [AnyTrue()](./collections/results/AnyTrue.md)                           | Determines if any boolean results in a collection are satisfied (true).                                                                                                  |
| [AnyFalse()](./collections/results/AnyFalse.md)                         | Determines if any boolean results in a collection are unsatisfied (false).                                                                                               |
| [GetAssertions()](./collections/results/GetAssertions.md)               | Aggregates all assertions from a collection of boolean results.                                                                                                          |
| [GetTrueAssertions()](./collections/results/GetTrueAssertions.md)       | Aggregates assertions from a collection of boolean results, but only includes those from satisfied (true) results.                                                     |
| [GetFalseAssertions()](./collections/results/GetFalseAssertions.md)     | Aggregates assertions from a collection of boolean results, but only includes those from unsatisfied (false) results.                                                  |
| [GetRootAssertions()](./collections/results/GetRootAssertions.md)       | Identifies the root cause boolean results in a complex evaluation and aggregates their assertions.                                                                       |
| [GetAllRootAssertions()](./collections/results/GetAllRootAssertions.md) | Aggregates assertions from all boolean results involved in an evaluation, regardless of their contribution to the final outcome.                                       |
| [AndTogether()](./collections/propositions/AndTogether.md)              | Creates a new proposition by performing a logical [And()](./operators/And.md) operation on all propositions in a collection. Also applicable to <xref:Motiv.BooleanResultBase`1>. |
| [AndAlsoTogether()](./collections/propositions/AndAlsoTogether.md)      | Creates a new proposition by performing a logical [AndAlso()](./operators/AndAlso.md) operation on all propositions in a collection. Also applicable to <xref:Motiv.BooleanResultBase`1>. |
| [OrTogether()](./collections/propositions/OrTogether.md)                | Creates a new proposition by performing a logical [Or()](./operators/Or.md) operation on all propositions in a collection. Also applicable to <xref:Motiv.BooleanResultBase`1>.   |
| [OrElseTogether()](./collections/propositions/OrElseTogether.md)     | Creates a new proposition by performing a logical [OrElse()](./operators/OrElse.md) operation on all propositions in a collection. Also applicable to <xref:Motiv.BooleanResultBase`1>.      |

## Expression Composition

Propositions built from a boolean predicate lambda via [`Spec.From()`](./builder/From.md) are *expression-backed*: alongside their usual explanatory decomposition, they retain a recoverable `Expression<Func<TModel, bool>>` that stays intact through composition with the logical operators, so it can be handed to a query provider (such as EF Core) for server-side translation instead of client-side evaluation. See [Expression Composition](./expression-composition/index.md) for the full composition and degradation rules.

| Method                                                            | Description                                                                                       |
|----------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------|
| [ToExpression()](./expression-composition/ToExpression.md)           | Recovers the composed `Expression<Func<TModel, bool>>` behind an expression-backed proposition.  |
| [Where()](./expression-composition/Where.md)                         | Filters an `IQueryable<TModel>` using an expression-backed proposition's predicate expression.   |

## Asynchronous Propositions

Propositions built from an async predicate via [`Spec.BuildAsync()`](./async/BuildAsync.md) compose rules that depend on I/O — databases, APIs, feature flags — with the same explainable results as synchronous propositions, and true short-circuiting of asynchronous work. Sync and async propositions compose freely in both directions, sequential evaluation is the default for thread-safety, and independent operands can opt into concurrent evaluation. See [Asynchronous Propositions](./async/index.md) for the full type hierarchy, composition rules, and cancellation semantics.

| Method                                                   | Description                                                                                       |
|-----------------------------------------------------------|-----------------------------------------------------------------------------------------------------|
| [BuildAsync()](./async/BuildAsync.md)                     | Initiates asynchronous proposition construction from an async predicate.                          |
| [EvaluateAsync()](./async/EvaluateAsync.md)               | Asynchronously evaluates a proposition, returning the same result types as synchronous evaluation. |
| [ToAsyncSpec()](./async/ToAsyncSpec.md)                   | Lifts a synchronous specification into the asynchronous hierarchy so it can compose with async operands. |
| [Concurrent Operators](./async/ConcurrentOperators.md)    | `AndConcurrently()`, `OrConcurrently()`, `XOrConcurrently()` — opt-in concurrent evaluation with results identical to the sequential form. |

## Observability

Motiv reports every top-level evaluation via OpenTelemetry &mdash; a `motiv.evaluate` span per `Evaluate()`/`EvaluateAsync()` call plus `motiv.evaluations`/`motiv.evaluation.duration` metrics &mdash; but emits nothing unless your application subscribes to the `"Motiv"` activity source and meter. See [Observability](./observability/index.md) for the full tag/metric reference, the `Matches`/`Where()` emission rules, and sensitive-data guidance.

## Live Rules

Live rules (in the `Motiv.Serialization` and `Motiv.Serialization.AspNetCore` packages) wrap serialized rule documents in typed, hot-swappable handles: declare a rule as a sealed class, inject the concrete type wherever the decision is made, and replace the implementation at runtime &mdash; through HTTP endpoints with optimistic concurrency, or directly through a `RuleSet` &mdash; without a restart and without tearing in-flight evaluations. See [Live Rules](./live-rules/index.md) for the four rule flavours, the concurrency model, and the async loading boundary.

| Type / Method                                                            | Description                                                                                       |
|---------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------|
| [Rule Classes](./live-rules/Rules.md)                                     | `Rule`, `PolicyRule`, `AsyncRule`, `AsyncPolicyRule` &mdash; declaring and evaluating live rules. |
| [RuleSet](./live-rules/RuleSet.md)                                        | Registers rules, binds defaults at startup, and applies `Update()`/`Revert()` with optimistic concurrency. |
| [RuleDocuments](./live-rules/RuleDocuments.md)                            | `FromJson()` and `Embedded()` &mdash; rule-document sources for rule defaults.                    |
| [ASP.NET Core Integration](./live-rules/AspNetCore.md)                    | `AddMotivRules()`, `AddRule()`, `MapMotivRules()`, and the `GET`/`PUT`/`DELETE` rule endpoints.   |
| [DeserializeAsyncSpec()](./live-rules/DeserializeAsyncSpec.md)            | Loads rule documents into the async hierarchy, lifting sync references and enforcing the sync/async boundary. |
