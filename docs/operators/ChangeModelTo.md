---
title: ChangeModelTo()
category: operators
---

A proposition is written against one model type, but the model you have at hand is often something else &mdash; the
proposition is about a `decimal` balance, and you are holding an `Account`. `ChangeModelTo()` re-points an existing
proposition at a different model, without rewriting it:

```csharp
ChangeModelTo<TNewModel>(Func<TNewModel, TModel> modelSelector)
```

The selector runs at evaluation time and takes the *new* model to the one the proposition already understands.

```csharp
var isOverdrawn = Spec.Build((decimal balance) => balance < 0).Create("is overdrawn");

var accountIsOverdrawn = isOverdrawn.ChangeModelTo((Account account) => account.Balance);

var result = accountIsOverdrawn.Evaluate(new Account(Balance: -5m));
// result.Satisfied  : true
// result.Reason     : "is overdrawn == true"
// result.Assertions : ["is overdrawn == true"]
```

The proposition is unchanged in every respect that matters to an explanation: the statement is still
`is overdrawn`, and the assertions are the ones the original proposition produces. Only the way it reaches its
subject has changed. This means a library of small, sharply-focused propositions written against primitive or
narrow types stays reusable across every model that can produce such a value.

## Narrowing to a derived model

The parameterless overload changes the model to a subtype, for which no selector is needed:

```csharp
ChangeModelTo<TDerivedModel>() where TDerivedModel : TModel
```

```csharp
var isNull = Spec.Build((object? o) => o is null).Create("is null");

var stringIsNull = isNull.ChangeModelTo<string?>();
```

## Composing propositions over differing models

This is what makes `ChangeModelTo()` more than a convenience. Propositions can only be combined with `&`, `|`,
`AndAlso()` and friends when they share a model type. Bringing each to a common model &mdash; typically one that
aggregates the inputs &mdash; lets otherwise-incompatible propositions compose into a single proposition:

```csharp
var isOverdrawn = Spec.Build((decimal balance) => balance < 0).Create("is overdrawn");
var isInStock = Spec.Build((Product p) => p.Stock > 0).Create("is in stock");

var canFulfil =
    isOverdrawn.ChangeModelTo((Order o) => o.Account.Balance).Not()
    & isInStock.ChangeModelTo((Order o) => o.Product);

var result = canFulfil.Evaluate(new Order(new Account(Balance: 10m), new Product(Stock: 0)));
// result.Satisfied  : false
// result.Reason     : "is in stock == false"
// result.Assertions : ["is in stock == false"]
```

Note the `Reason`: only `is in stock == false` appears, because the overdraft check passed and therefore did not
contribute to the outcome. De-noising works across a `ChangeModelTo()` boundary exactly as it does anywhere else.

The alternative is to compose the *results* instead (`resultA & resultB`), which also works across differing
models. The difference is that `ChangeModelTo()` yields a **proposition** &mdash; reusable, composable further, and
evaluated as a single decision &mdash; whereas composing results combines two decisions after the fact. Prefer
`ChangeModelTo()` when the combined rule is a thing you want to name, reuse, or
[trace](../observability/index.md).

## Policies

`ChangeModelTo()` preserves the policy guarantee: called on a
[`PolicyBase<TModel, TMetadata>`](../builder/Policy%602.md), it returns a policy, so a single-value proposition
stays single-valued after the model changes.

## Availability

`ChangeModelTo()` is defined on the synchronous proposition types (`SpecBase<TModel, TMetadata>` and
`PolicyBase<TModel, TMetadata>`). There is no asynchronous equivalent &mdash; re-point the proposition before
lifting it into the [asynchronous hierarchy](../async/index.md).
