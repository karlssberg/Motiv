# Queryable Expression Composition — Design

**Date:** 2026-07-04
**Status:** Approved for planning

## Problem

Motiv propositions built from lambda expression trees (`Spec.From(...)`) retain the original
`Expression<Func<TModel, bool>>` at runtime, but there is no public way to recover it. The only
spec-to-predicate conversion is the implicit `Func<TModel, bool>` operator, which returns a
compiled delegate — opaque to query providers, forcing client-side evaluation in EF Core and
similar translators.

This feature makes composed propositions able to surrender a composed
`Expression<Func<TModel, bool>>` suitable for query providers, while preserving Motiv's
explanation semantics untouched.

## Decisions (from design discussion)

| Question | Decision |
|---|---|
| Consumption surface | Layered: core `ToExpression()` plus thin `IQueryable.Where(spec)` conveniences |
| Non-expression leaves | Compile-time separation — a distinct type hierarchy; impossible to ask a delegate-backed spec for an expression |
| Composition semantics | Closed composition + degrade: expression × expression → expression-backed; expression × ordinary → ordinary (like Policy → Spec) |
| Entry points | All `Spec.From(Expression<Func<TModel, bool>>)` builder paths (minimal, explanation, metadata, Yield variants) |
| Operator mapping | Faithful: `And`→`Expression.And`, `AndAlso`→`Expression.AndAlso`, `Or`→`Expression.Or`, `OrElse`→`Expression.OrElse`, `XOr`→`Expression.ExclusiveOr`, `Not`→`Expression.Not` |
| Higher-order propositions | Deferred — `AsAllSatisfied`/`AsAnySatisfied` keep returning ordinary specs |
| Mixed metadata | Stays expression-backed: returns `ExpressionSpecBase<TModel, string>` |
| Naming | `ExpressionSpecBase` / `ExpressionPolicyBase` / `IExpressionSpec<TModel>` |
| Decomposed tree | Also expression-backed: `Transform()` returns `ExpressionSpecBase<TModel, string>`; every decomposed node carries its source sub-expression |

## Type Hierarchy

```
SpecBase<TModel, TMetadata>
├── PolicyBase<TModel, TMetadata>
│   └── ExpressionPolicyBase<TModel, TMetadata>   ─┐
└── ExpressionSpecBase<TModel, TMetadata>         ─┴─ both implement IExpressionSpec<TModel>
```

```csharp
public interface IExpressionSpec<TModel>
{
    Expression<Func<TModel, bool>> ToExpression();
}
```

Policy-ness and expression-ness are independent axes; C# single inheritance forces two abstract
classes, unified by `IExpressionSpec<TModel>` for consumers that only need the expression.

### `ExpressionSpecBase<TModel, TMetadata>` (`: SpecBase<TModel, TMetadata>`)

- `public abstract Expression<Func<TModel, bool>> ToExpression();`
- Shadowed composition methods closed over expression-backed operands, following the
  `PolicyBase.Not()`/`OrElse()` shadowing precedent:
  - `And`, `AndAlso`, `Or`, `OrElse`, `XOr` accepting `ExpressionSpecBase<TModel, TMetadata>`,
    returning `ExpressionSpecBase<TModel, TMetadata>`
  - `new Not()` returning `ExpressionSpecBase<TModel, TMetadata>`
  - Operators `&`, `|`, `^`, `!` likewise
- Mixed-metadata overloads (see below)

### `ExpressionPolicyBase<TModel, TMetadata>` (`: PolicyBase<TModel, TMetadata>`)

Same surface. Mirrors the existing policy-preservation rule, lifted onto the expression axis:

- `Not()` → `ExpressionPolicyBase` (via internal `ExpressionNotPolicy`)
- `OrElse(ExpressionPolicyBase)` → `ExpressionPolicyBase` (via internal `ExpressionOrElsePolicy`)
- All other operators → `ExpressionSpecBase`

### Internal composites

Six spec composites — `ExpressionAndSpec`, `ExpressionAndAlsoSpec`, `ExpressionOrSpec`,
`ExpressionOrElseSpec`, `ExpressionXOrSpec` (implementing `IBinaryOperationSpec<TModel, TMetadata>`)
and `ExpressionNotSpec` (implementing `IUnaryOperationSpec`) — plus `ExpressionNotPolicy` and
`ExpressionOrElsePolicy`. Each mirrors its existing counterpart's evaluation exactly and reuses
the same internal description types, so `Reason`, `Justification`, `Assertions`, de-noising, and
`Underlying` are byte-identical to the ordinary composites. Per the project's "avoid over-DRYing"
norm, mirroring the small evaluation logic is acceptable; forwarding to an inner ordinary
composite is an equally acceptable implementation choice if it keeps output identical.

### Expression-carrying adapter

An internal adapter deriving `ExpressionSpecBase<TModel, string>` that wraps any
`SpecBase<TModel>` for evaluation (forwarding `Evaluate`, description, underlying) and returns a
supplied `Expression<Func<TModel, bool>>` from `ToExpression()`. Used for:

1. Metadata coercion (the expression-preserving analogue of `ToExplanationSpec()`)
2. Decomposed subtrees that are not natively expression-backed (inline `Any`/`All` higher-order
   decompositions, bool ternaries)

## Expression Recombination

- **Leaves** return the original lambda exactly as the user wrote it — no rewriting, no
  decomposition round-trip.
- **Composites** combine lazily on first `ToExpression()` call:
  1. Recursively obtain child expressions.
  2. Rebind the right child's parameter to the left's with a `ParameterReplacementVisitor`
     (an `ExpressionVisitor` substituting one `ParameterExpression` for another).
  3. Combine with the faithful operator mapping.
  4. Memoize in `Lazy<Expression<Func<TModel, bool>>>` — thread-safe, built once per composite.
- **No simplification** (double-negation elimination, De Morgan) is performed; providers handle
  `Not` nodes natively and the tree stays faithful to what the user composed.
- A compiled recombined expression behaves identically to the spec's own boolean evaluation
  semantics (faithful mapping preserves short-circuit vs non-short-circuit behavior).

## Entry Points

All `Spec.From(Expression<Func<TModel, bool>>)` builder paths return the expression-backed types:
paths that today produce `PolicyBase` produce `ExpressionPolicyBase`; paths that produce
`SpecBase` produce `ExpressionSpecBase`.

- Mechanically: new bool-specific `[FluentMethod("From")]` factory structs beside the existing
  generic `TPredicateResult` ones, feeding bool-specialized leaf classes re-based from the four
  existing `ExpressionTree*Proposition` types.
- The `Expression<Func<TModel, BooleanResultBase<string>>>` and `PolicyResultBase` overloads are
  untouched — their lambdas are not boolean predicates and can never translate to SQL.
- C# overload resolution must prefer the bool-specific `From` for plain predicate lambdas.
  **This is the riskiest mechanical piece — verify with a compile spike first** (see Testing #2).
- Non-breaking: the new types are subtypes of the old declared types.

## Decomposed Tree Is Expression-Backed

`ExpressionTreeTransformer<TModel>.Transform()` returns `ExpressionSpecBase<TModel, string>`
(previously `SpecBase<TModel, string>`), and every node it produces is expression-backed:

- **Atomic leaves** (comparisons, quasi-propositions): retain
  `Expression.Lambda<Func<TModel, bool>>(subExpressionNode, originalParameter)` — the atom's
  `ToExpression()` returns exactly the fragment of the original lambda it explains.
- **Interior composites**: fall out automatically from closed composition (the transformer
  composes with `.And()`/`.AndAlso()`/`.OrElse()` etc. in string metadata).
- **Inline `Any`/`All` and bool ternaries**: decompose to higher-order/composed specs as today,
  wrapped in the expression-carrying adapter with their source sub-expression. Explanation
  flows through the wrapped spec; `ToExpression()` returns the sub-expression. The deferred
  higher-order hierarchy is untouched.

Consequences:

1. Public `ToSpec()` extension (`ExpressionTreeExtensions`) now returns
   `ExpressionSpecBase<TModel, string>` — non-breaking (subtype).
2. Causal use case: each atomic cause is an `IExpressionSpec<TModel>`, so a consumer can walk a
   result's causes and recover the expressions of exactly the sub-propositions that determined
   the outcome — turning "why did this fail" into a query for other rows that fail the same way.

**Explanation semantics do not change**: same decomposition, same assertions, same de-noising.
The outer `Spec.From` proposition's `ToExpression()` returns the pristine original lambda;
decomposed nodes' expressions exist for interacting with the parts. Decomposition for
explanation is unchanged; `ToExpression()` bypasses decomposition — do not regress this during
implementation.

## Mixed Metadata

Combining `ExpressionSpecBase<TModel, TMetadataA>` with `ExpressionSpecBase<TModel, TMetadataB>`
returns `ExpressionSpecBase<TModel, string>` (string coercion as today, expression axis intact):

```csharp
public ExpressionSpecBase<TModel, string> And<TSpec>(TSpec spec)
    where TSpec : SpecBase<TModel>, IExpressionSpec<TModel> => ...
```

- Generic method overloads with the dual constraint are fully type-safe (no downcasts).
  Overload resolution prefers the non-generic same-metadata overload when both apply.
- Coercion uses the expression-carrying adapter.
- **Operator forms** (`&`, `|`, `^`): operators cannot be generic; cross-metadata closure
  requires operator overloads taking `IExpressionSpec<TModel>` as one parameter. Verify
  resolution with a compile spike. Fallback if ambiguous: method forms preserve expression-ness
  across metadata types; operator forms require matching metadata (documented, consistent with
  the existing `&&`/`||` limitation).

## IQueryable Conveniences

In core Motiv (no new package or dependency — `IQueryable` is `System.Linq`):

```csharp
public static class QueryableExtensions
{
    public static IQueryable<TModel> Where<TModel>(
        this IQueryable<TModel> source,
        IExpressionSpec<TModel> spec) =>
        source.Where(spec.ToExpression());
}
```

A single `IExpressionSpec<TModel>`-based overload covers both abstract classes.

## Degradations (by design, silent in the static type — like Policy → Spec)

- Expression-backed × ordinary spec → ordinary composite (inherited base overloads bind).
- `ChangeModelTo<TDerived>()` → ordinary spec (expression parameter-type remapping deferred).
- `AsAllSatisfied`/`AsAnySatisfied` → ordinary higher-order specs (deferred; inline `Any`/`All`
  within a single `Spec.From` lambda still works — the whole lambda is one leaf).

## Error Handling

No new runtime failure modes. Compile-time separation removes the "expression unavailable"
error path entirely; `ToExpression()` cannot throw; the parameter rebinder is total. No runtime
validation of provider translatability — that remains the query provider's job, and failures
point at the user's own lambdas.

## Testing (TDD throughout; xUnit in Motiv.Tests)

1. **Leaf round-trip** — every `Spec.From(bool-predicate)` builder path returns the
   expression-backed type; `ToExpression()` returns the original lambda unchanged.
2. **Entry-point resolution (compile spike, written first)** — plain predicate lambdas bind to
   the bool-specific `From` overloads; `BooleanResultBase`/`PolicyResultBase` lambdas keep the
   existing generic path and ordinary types.
3. **Per-operator composition** — correct `ExpressionType` node; operands rebound to a single
   shared parameter; compiled composed expression agrees with `spec.Matches` across full truth
   tables (`[Theory]`).
4. **Semantic-faithfulness regression** — `Reason`, `Justification`, `Assertions` from
   expression-backed compositions are byte-identical to ordinary-spec compositions. Full
   solution test suite, including example projects, before completion.
5. **Type-axis preservation** — `Not`/`OrElse` on `ExpressionPolicyBase` stay expression-policies;
   other operators return `ExpressionSpecBase`; mixed metadata returns
   `ExpressionSpecBase<TModel, string>` with expression intact; degradations degrade.
6. **Decomposed tree** — `Transform()` nodes are expression-backed; atomic leaves return their
   source fragments; wrapped `Any`/`All`/ternary nodes return their sub-expressions and explain
   identically to today.
7. **Memoization** — repeated `ToExpression()` calls return the same instance.
8. **IQueryable integration** — `Where(spec)` against `AsQueryable()` in Motiv.Tests, plus a new
   example test project `src/examples/Motiv.EntityFramework.Tests` (net8.0+, EF Core, SQLite
   in-memory) proving end-to-end provider translation: server-side `WHERE` from a composed
   spec, no client evaluation. (`EnumerableQuery` compiles delegates and would mask translation
   failures — the EF project is the real proof.)

## Documentation

- `README.md`: brief example under Core Features.
- New `docs/expression-composition/` section: `index.md`, `ToExpression.md`, `Where.md`,
  `toc.yml`; wired into `docs/toc.yml` and `docs/Overview.md`.
- Update `docs/builder/From.md`: `From` now yields queryable-composable propositions.

## Risks / Spikes (do first)

1. Overload resolution: bool-specific `From` vs generic `TPredicateResult` `From`.
2. Operator overloads taking `IExpressionSpec<TModel>` vs inherited `SpecBase<TModel>` operators
   — ambiguity check for cross-metadata `&`/`|`/`^`.
3. Fluent source generator (Converj) behavior when adding sibling factory structs sharing the
   `"From"` fluent method name.
