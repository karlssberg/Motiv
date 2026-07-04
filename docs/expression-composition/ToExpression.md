---
title: ToExpression()
---

`ToExpression()` recovers the predicate expression tree behind an expression-backed proposition, suitable for
handing to a query provider or composing with other `System.Linq.Expressions` code.

```csharp
Expression<Func<TModel, bool>> ToExpression();
```

It is declared on the `IExpressionSpec<TModel>` interface, and implemented by both
`ExpressionSpecBase<TModel, TMetadata>` and `ExpressionPolicyBase<TModel, TMetadata>` &mdash; the two base types
that every `Spec.From()` builder path returns. See [Expression Composition](index.md) for how a proposition
becomes expression-backed in the first place.

## Remarks

- **Leaves return the original lambda instance.** A proposition created directly from `Spec.From()` returns
  exactly the `Expression<Func<TModel, bool>>` you passed in &mdash; no rewriting, no decomposition round-trip.
- **Composites recombine lazily and memoize.** A proposition built by composing expression-backed operands (with
  `And()`, `Or()`, `Not()`, etc.) does not build its combined expression until `ToExpression()` is first called.
  From then on, the result is memoized, so repeated calls return the same `Expression<Func<TModel, bool>>`
  instance rather than rebuilding it.
- **No simplification is applied.** The composed tree faithfully mirrors the operators used to build it &mdash;
  there's no De Morgan's-law rewriting or double-negation elimination. This keeps the tree predictable and lets
  query providers handle negation and short-circuiting nodes natively.

## Basic Example

```csharp
var isAdult = Spec.From((Customer c) => c.Age >= 18).Create("is adult");

Expression<Func<Customer, bool>> predicate = isAdult.ToExpression();
// c => c.Age >= 18 — the original lambda, unchanged
```

## Composed Example

```csharp
var isAdult  = Spec.From((Customer c) => c.Age >= 18).Create("is adult");
var isActive = Spec.From((Customer c) => c.IsActive).Create("is active");

var eligible = isAdult & isActive;

Expression<Func<Customer, bool>> predicate = eligible.ToExpression();
// c => c.Age >= 18 And c.IsActive
```

## Recovering the Expression Behind a Decomposed Cause

Every node produced while decomposing a `Spec.From()` expression &mdash; including atomic sub-expressions used to
explain a result &mdash; is itself expression-backed. This lets you recover the expression of just the
sub-proposition that caused a particular outcome, and reuse it independently:

```csharp
Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;
var spec = expression.ToSpec();

var binary = (Motiv.Traversal.IBinaryOperationSpec<Customer, string>)spec;
var ageAtom = (IExpressionSpec<Customer>)binary.Left;

Expression<Func<Customer, bool>> ageExpression = ageAtom.ToExpression();
// c => c.Age >= 18

var customersMissingAge = dbContext.Customers.Where(ageAtom.ToExpression());
```

## Next Steps

- Use [`Where()`](Where.md) to apply a recovered expression directly to an `IQueryable<TModel>`.
- Read the [Expression Composition](index.md) overview for the composition and degradation rules that determine
  whether a given proposition is expression-backed.
