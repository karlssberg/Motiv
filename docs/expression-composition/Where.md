---
title: Where()
---

`Where()` filters an `IQueryable<TModel>` using an expression-backed proposition's predicate expression, so query
providers such as EF Core can translate the proposition into server-side query logic (e.g. SQL) instead of
evaluating it client-side.

```csharp
public static IQueryable<TModel> Where<TModel>(
    this IQueryable<TModel> source,
    IExpressionSpec<TModel> spec);
```

It is an extension method in the `Motiv` namespace, defined in `QueryableExtensions`, and accepts anything
implementing `IExpressionSpec<TModel>` &mdash; which covers both `ExpressionSpecBase<TModel, TMetadata>` and
`ExpressionPolicyBase<TModel, TMetadata>`, and therefore every proposition returned from a `Spec.From()` builder
path (as long as it remains expression-backed &mdash; see [Expression Composition](index.md) for the composition
and degradation rules).

## Remarks

`Where()` passes `spec.ToExpression()` to the query provider verbatim &mdash; it does not compile the expression
into a delegate first. This is what allows a provider like EF Core to translate the proposition's logic into SQL
rather than pulling every row into memory and filtering client-side.

## Example

```csharp
var isAdult  = Spec.From((Customer c) => c.Age >= 18).Create("is adult");
var isActive = Spec.From((Customer c) => c.IsActive).Create("is active");

var eligible = isAdult & isActive;

var customers = dbContext.Customers.Where(eligible);
var sql = customers.ToQueryString(); // contains a WHERE clause — proof of server-side translation
```

## Verified with EF Core

This behavior is proven end-to-end (not merely asserted) against EF Core with a SQLite provider in
`src/examples/Motiv.EntityFramework.Tests`, including composed and negated propositions, and re-querying with an
expression recovered from a decomposed cause:

```csharp
var isSenior   = Spec.From((Customer c) => c.Age >= 65).Create("is senior");
var isInactive = Spec.From((Customer c) => !c.IsActive).Create("is inactive");
var needsReview = isSenior.OrElse(isInactive);
var fine = !needsReview;

var act = dbContext.Customers.Where(fine).Select(c => c.Id).ToArray();
```

## Next Steps

- Use [`ToExpression()`](ToExpression.md) directly when you need the raw expression rather than a filtered
  `IQueryable<TModel>` &mdash; for example, to compose it further with `System.Linq.Expressions` code.
- Read the [Expression Composition](index.md) overview for the composition and degradation rules that determine
  whether a given proposition is expression-backed.
