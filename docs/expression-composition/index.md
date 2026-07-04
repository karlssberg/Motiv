---
title: Expression Composition
description: Documentation for expression-backed propositions in Motiv, which compose into query-provider-translatable expression trees.
---

`Spec.From()` propositions built from a boolean predicate lambda (`Expression<Func<TModel, bool>>`) do more than
decompose their expression tree into explanatory assertions &mdash; they also retain a recoverable
`Expression<Func<TModel, bool>>` that can be composed with the usual logical operators and handed to a query
provider (such as EF Core) for server-side translation. This closes the gap between "a proposition that explains
itself" and "a predicate that a database can execute".

## Expression-Backed Propositions

A proposition is *expression-backed* when it exposes its predicate as a recoverable expression tree, via the
`IExpressionSpec<TModel>` interface:

```csharp
public interface IExpressionSpec<TModel>
{
    Expression<Func<TModel, bool>> ToExpression();
}
```

Two abstract classes implement it, mirroring the existing `SpecBase`/`PolicyBase` hierarchy:

| Type                                                  | Base type                                | Guarantees                                                          |
|--------------------------------------------------------|-------------------------------------------|----------------------------------------------------------------------|
| `ExpressionSpecBase<TModel, TMetadata>`                | `SpecBase<TModel, TMetadata>`             | Expression-backed; may yield multiple assertions/metadata per evaluation |
| `ExpressionPolicyBase<TModel, TMetadata>`              | `PolicyBase<TModel, TMetadata>`           | Expression-backed *and* resolves to exactly one assertion/metadata value  |

Policy-ness and expression-ness are independent axes &mdash; a proposition can be a policy, expression-backed,
both, or neither. All `Spec.From()` builder paths (minimal, explanation, metadata, and their `Yield` variants)
return one of these two types instead of the ordinary `SpecBase`/`PolicyBase`, so no code changes are required to
start recovering expressions &mdash; existing `Spec.From()` propositions already are expression-backed.

## Closed Composition

Combining two expression-backed propositions over the same model type with `And()`, `AndAlso()`, `Or()`,
`OrElse()`, `XOr()`, or `Not()` (and their `&`, `&&`, `|`, `||`, `^`, `!` operator equivalents) produces another
expression-backed proposition. The resulting `ToExpression()` recombines both operands' expressions into a single
tree, so a whole hierarchy of composed propositions still yields one expression that a query provider can consume.

```csharp
var isAdult  = Spec.From((Customer c) => c.Age >= 18).Create("is adult");
var isActive = Spec.From((Customer c) => c.IsActive).Create("is active");

var eligible = isAdult & isActive; // still expression-backed

Expression<Func<Customer, bool>> predicate = eligible.ToExpression();
// c => c.Age >= 18 And c.IsActive
```

This mirrors the existing **Policy Preservation** rule (see [Policy vs Spec](../builder/Spec.md)): just as `!policy`
and `policy.OrElse(policy)` keep returning a policy, `Not()` and `OrElse()` on an `ExpressionPolicyBase` keep
returning an `ExpressionPolicyBase`. Every other operator &mdash; on either policies or specs &mdash; returns an
`ExpressionSpecBase`.

### Faithful Operator Mapping

Each Motiv operator maps directly onto its `System.Linq.Expressions` equivalent &mdash; there is no rewriting or
simplification of the resulting tree (no De Morgan's laws, no double-negation elimination). The composed
expression stays faithful to exactly what was composed, which is what query providers expect to translate.

| Motiv operator      | Expression node produced |
|----------------------|---------------------------|
| `And()` / `&`         | `Expression.And`           |
| `AndAlso()` / `&&`    | `Expression.AndAlso`        |
| `Or()` / `\|`          | `Expression.Or`             |
| `OrElse()` / `\|\|`    | `Expression.OrElse`         |
| `XOr()` / `^`         | `Expression.ExclusiveOr`    |
| `Not()` / `!`         | `Expression.Not`            |

## Degradation

Not every composition can stay expression-backed. When a query provider can no longer be handed a faithful
predicate, a composition silently degrades to an ordinary (non expression-backed) proposition &mdash; the same way
combining a `PolicyBase` with a plain `SpecBase` degrades from a policy to a spec. The degradation is silent in the
sense that it happens through ordinary C# overload resolution and shows up only in the static type of the result;
no exception is thrown and no data is lost, but `ToExpression()` is no longer available.

This happens when an expression-backed proposition is composed with:

- **An ordinary (non-expression-backed) proposition** &mdash; its predicate cannot be recovered as an expression,
  so the composite as a whole can't be either.
- **`ChangeModelTo<TDerived>()`** &mdash; expression parameter-type remapping isn't performed, so the result is an
  ordinary proposition over the new model type.
- **Higher-order propositions** &mdash; `AsAllSatisfied()`, `AsAnySatisfied()`, and the other collection-based
  builder methods return ordinary specs.

Inline `Any`/`All` calls *inside* a single `Spec.From()` lambda are unaffected by this &mdash; the whole lambda is
still just one leaf as far as expression recovery is concerned:

```csharp
var allPositive = Spec
    .From((IEnumerable<int> numbers) => numbers.All(n => n > 0))
    .Create("all positive");

Expression<Func<IEnumerable<int>, bool>> predicate = allPositive.ToExpression();
// numbers => numbers.All(n => n > 0) — the original lambda, unchanged
```

## Mixed Metadata

Combining two expression-backed propositions whose `TMetadata` types differ behaves differently depending on
whether you use the method form or the operator form:

- **Method forms** (`And()`, `AndAlso()`, `Or()`, `OrElse()`, `XOr()`) stay expression-backed. Both operands are
  coerced to string metadata (the same coercion ordinary propositions already use when mixing metadata types), and
  the result is an `ExpressionSpecBase<TModel, string>` with its composed expression intact.
- **Operator forms** (`&`, `|`, `^`) require the operands to share the same `TMetadata` type; if they don't, the
  operator overloads that close over expression-backed operands no longer apply, and the composition falls back to
  the ordinary (ordinary in this context includes non-string generic) inherited operator, which degrades to a
  non-expression-backed result. This mirrors the existing limitation that `&&`/`||` (`AndAlso`/`OrElse`) operators
  are only available on results and within expression trees, and not on propositions with mismatched metadata
  &mdash; see [Logical Operators](../operators/index.md).

## Explanations vs. `ToExpression()`

Explanations are unaffected by any of this: they are still produced by decomposing the original lambda into
sub-propositions, exactly as documented in [`Spec.From()`](../builder/From.md). `ToExpression()` is a separate,
parallel path &mdash; for a leaf proposition it bypasses decomposition entirely and returns the pristine original
lambda instance that was passed to `Spec.From()`.

## Recovering Sub-Expressions from Decomposed Causes

Because every node produced when decomposing a `Spec.From()` expression is itself expression-backed &mdash; atomic
comparisons, inlined `Any`/`All` calls, and ternaries alike &mdash; you can walk the causes of a result and recover
the expression of exactly the sub-propositions that determined the outcome. The `expression.ToSpec()` extension
method (see [`Spec.From()`](../builder/From.md)) returns an `ExpressionSpecBase<TModel, string>` for this purpose:

```csharp
Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;
var spec = expression.ToSpec();

var binary = (Motiv.Traversal.IBinaryOperationSpec<Customer, string>)spec;
var ageAtom = (IExpressionSpec<Customer>)binary.Left;

Expression<Func<Customer, bool>> ageExpression = ageAtom.ToExpression();
// c => c.Age >= 18 — exactly the sub-expression that caused this branch
```

This turns "why did this evaluation fail" into a query: take the expression of the exact sub-proposition that
caused a result and re-run it against other rows to find everything else that fails for the same reason.

## Available Methods

| Method                          | Description                                                                                       |
|----------------------------------|-----------------------------------------------------------------------------------------------------|
| [ToExpression()](ToExpression.md) | Recovers the composed `Expression<Func<TModel, bool>>` behind an expression-backed proposition.  |
| [Where()](Where.md)               | Filters an `IQueryable<TModel>` using an expression-backed proposition's predicate expression.   |

## Next Steps

- Read about [`Spec.From()`](../builder/From.md), the entry point for expression-backed propositions.
- Explore the [Logical Operators](../operators/index.md) used to compose propositions.
- See [`ToExpression()`](ToExpression.md) and [`Where()`](Where.md) for the two methods this feature adds.
