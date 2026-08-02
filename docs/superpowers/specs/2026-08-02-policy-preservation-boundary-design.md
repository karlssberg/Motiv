# Policy Preservation Boundary

**Date:** 2026-08-02
**Status:** Approved

## Context

`PolicyBase<TModel, TMetadata>.OrElse` returns a policy, so an `OrElse` chain behaves like an
if/else chain that yields a single value even when nothing matched. The proposal was to extend that
policy preservation to the operator overloads (`|`, and `||` on results), so the whole OR family
behaved consistently.

Investigating raised three questions that had to be answered before the proposal could be judged:
whether operators *can* carry policy preservation, whether `OrElse` deserves it, and whether the
places that currently lack it are gaps or deliberate.

## Findings

All findings below were verified by probing the shipped behaviour, not by reading the overloads.

**1. `OrElse` already implements the if/else-chain semantics, and `Value` diverges from `Values`.**

```
left.OrElse(right), both unsatisfied:
  Satisfied = False
  Value     = [right-false]                 // last evaluated wins
  Values    = [left-false, right-false]     // both are genuine causes
```

`OrElsePolicyResult.Value` is `(Right ?? Left).Value`. The single-value guarantee is upheld by
*selecting* one of two real causes, so `Value != Values.Single()` for an unsatisfied chain.

**2. `OrElse` preserves policies correctly everywhere.** Every policy-with-policy combination
returns a policy, including expression/plain mixes:

| call | static type | runtime type |
|---|---|---|
| `plainPolicy.OrElse(plainPolicy)` | `PolicyBase<T,M>` | `OrElsePolicy` |
| `metadataPolicy.OrElse(metadataPolicy)` | `PolicyBase<T,int>` | `OrElsePolicy` |
| `exprPolicy.OrElse(exprPolicy)` | `ExpressionPolicyBase<T,M>` | `ExpressionOrElsePolicy` |
| `exprPolicy.OrElse(plainPolicy)` | `PolicyBase<T,M>` | `OrElsePolicy` |
| `plainPolicy.OrElse(exprPolicy)` | `PolicyBase<T,M>` | `OrElsePolicy` |

There is no bug here. The `new`-redeclarations on `ExpressionPolicyBase` that exist to preserve
declaring-type precedence do their job.

**3. `Spec.From(expr).Create("name")` returns a Spec, and that is correct.** Unlike
`Spec.Build(pred).Create("name")`, the minimal expression-tree form has no supplied value, so its
values are the decomposed clauses — plural whenever more than one clause is causal:

```
Spec.From((int n) => n > 0 || n > 100).Create("in range").Evaluate(-5)
  minimal                          -> Values = [n <= 0, n <= 100]   (2)
  .WhenTrue("yes").WhenFalse("no") -> Value  = [no]                 (1)
```

It fails the single-value contract on its own merits. Supplying `WhenTrue`/`WhenFalse` supplies the
single value and it becomes an `ExpressionPolicyBase`, as it should.

## Decisions

### 1. No policy-preserving operator overloads

C# cannot overload `||` directly. `x || y` compiles to `T.false(x) ? x : T.|(x, y)`, and the selected
`operator |` must take *and* return exactly `T`. Making `||` policy-preserving therefore forces
making `|` policy-preserving — but `|` is eager `Or`, where both operands always evaluate and no
operand is canonical. Under the `(Right ?? Left)` rule, `satisfiedPolicy | unsatisfiedPolicy` would
report `Satisfied = true` while returning the *unsatisfied* operand's value.

Two further consequences, each independently disqualifying:

- `x || y` short-circuits by returning `x` **itself, unwrapped** — no `OrElse` node in the
  justification tree, so the operator and the method would produce different result trees.
- An `operator |` on `PolicyBase` that *meant* `OrElse` would make `|` eager on specs and lazy on
  policies. Since `PolicyBase : SpecBase`, widening a variable's declared type would silently change
  evaluation semantics — side effects, exceptions and cost.

`docs/operators/OrElse.md` currently attributes this to "quirks regarding the overloading of the `||`
operator". That is to be replaced with the actual mechanism.

### 2. `AndAlso` stays a Spec

The mechanical case for a mirror is real: `AndAlso` is single-valued when unsatisfied exactly as
`OrElse` is when satisfied, and `AndAlsoPolicyResult.Value` would be the identical expression,
`(Right ?? Left).Value`. It is still rejected.

Policy status is not a property of the algebra. The test is whether the combinator has a **canonical
single value a caller would ask for**:

- `OrElse` does. It is `??` — the first operand that matched, or the final fallback. Its name
  advertises selection, and the idiom is universally understood.
- `AndAlso` does not. "Also" advertises accumulation: in its satisfied direction both operands are
  causal and both are the point, so nominating one discards what the name emphasises. Its
  single-valued direction is the *unsatisfied* one ("which gate failed?"), which is a different
  operation wearing conjunction's name.
- `Not` does, trivially — one operand in, one out.
- Eager `Or`/`And`/`XOr` do not — no operand is distinguished.

If "first failure wins" is wanted later it gets its own name; it does not get conjunction's.

### 3. Policy preservation is a static-type property, uniformly

`policy.OrElse(spec)` returns a Spec. Declaring policies as `IEnumerable<SpecBase<T,M>>` and calling
`OrElseTogether()` is the same act, and returns an `OrElseSpec` rather than an `OrElsePolicy`. This
is not a covariance defect to be worked around — introduce a non-policy and you abandon policy
preservation. The rule is uniform and should be stated as such.

### 4. The unselected causes must stay reachable

If `Value` is a *selection*, a consumer must have a clean way to obtain everything it selected from.
`Values` already provides this, verified across the cases that matter:

```
3-deep chain, all false : Value = [c-false]   Values = [a-false | b-false | c-false]   (3)
mixed, middle satisfied : Value = [y-true]    Values = [y-true]                        (1)
non-string metadata     : Value = [30]        Values = [10 | 20 | 30]                  (3)
```

Three properties hold and are to be guaranteed:

- **It flattens.** An `OrElseTogether` chain is a left-nested tree of `OrElsePolicyResult`, but
  `Values` recurses rather than reporting only the root's two operands.
- **It de-noises.** Only causal values appear. When the middle policy is satisfied, `Values` is just
  that policy's value — the non-contributing operands are correctly absent.
- **It is metadata-agnostic.** It works for arbitrary `TMetadata`, not only the string path.

No new member is added. `Values` is the uniform property across every result type; a policy-only
synonym would fragment the API and cut against Policy being a subtype of Spec. The problem is
discoverability — `Value` and `Values` differ by one character while carrying different contracts —
and it is addressed by documentation and tests.

Note for the docs: `Underlying` and `Causes` report the *binary composition shape* (2 operands for a
3-policy chain), whereas `Values` and `Assertions` flatten to the causal set (3). Both are
legitimate readings of "all the values"; the flattening one is what a consumer reaching for
"give me everything" expects, so the distinction must be stated rather than discovered.

### 5. Correct the documented Policy contract

CLAUDE.md states that a Policy "resolves to a single value" and "guarantees exactly one value". That
overstates what ships, per finding 1. The contract to document:

> A Policy guarantees a single `Value`. For an `OrElse` composition that value is the last-evaluated
> operand's — the `??` fallback — and when a chain is unsatisfied, `Values` and `Assertions` still
> report every contributing cause. `Value` is therefore not necessarily `Values.Single()`: it is a
> selection, and `Values` is how a consumer reaches everything it was selected from.

## Scope of Change

No production code changes. This is a documentation correction plus a regression pin.

| File | Change |
|---|---|
| `CLAUDE.md` | Correct the Policy vs Spec contract wording (lines ~160, ~163). Add the canonical-single-value rule and the static-type rule to Policy Preservation (~line 125). Record the `\|\|` mechanism so operator overloads are not re-proposed. |
| `docs/operators/OrElse.md` | Replace "quirks regarding the overloading of the `\|\|` operator" with the mechanism. Add a Policies section covering `Value` on an unsatisfied chain, `Values` as the way to reach every cause, and the flattening-vs-shape distinction against `Causes`/`Underlying`. |
| `src/Motiv.Tests/OrElsePolicyTests.cs` | Pin `Value` = last-evaluated while `Values` reports both causes. |
| `src/Motiv.Tests/PolicyExtensionsTests.cs` | Pin the three `Values` guarantees on a chain: flattening (3-deep, all false, 3 values), de-noising (middle satisfied, 1 value), and non-string metadata. |

## Non-Goals

- Adding `|`, `&`, `^`, `||`, `&&` overloads to `PolicyBase` or `PolicyResultBase`.
- Adding a policy-only member for "all values". `Values` already serves this and is uniform across
  every result type.
- Adding `AndAlso`/`AndAlsoTogether` policy overloads.
- Changing `OrElse`'s runtime behaviour, which is correct and already pinned by
  `PolicyExtensionsTests`.
- Changing the `Build`/`From` minimal-proposition asymmetry, which is correct per finding 3.

## Verification

Run the full solution test suite, not just `Motiv.Tests` — the example projects assert on
justification strings. Per project convention:

```
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"
dotnet test
```

The new test characterises existing behaviour, so it passes on first run. TDD's red step does not
apply; instead confirm it is actually load-bearing by temporarily changing
`OrElsePolicyResult.Value` to `Left.Value` and checking the test fails, then reverting.
