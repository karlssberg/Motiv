---
title: AsAllSatisfied()
category: building
---

# All satisfied

```csharp
AsAllSatisfied()
```

The proposition is satisfied if all models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAllSatisfied()
    .Create("all are even")
```

## Performance

When you only need the boolean outcome, call `Matches(models)` instead of `Evaluate(models)`. For the built-in higher-order operations (`AsAllSatisfied`, `AsAnySatisfied`, `AsNoneSatisfied`, `AsAtLeastNSatisfied`, `AsAtMostNSatisfied`, `AsNSatisfied`), `Matches` short-circuits — it stops evaluating models as soon as the outcome is decided — and skips the higher-order result materialization that `Evaluate` performs.

For propositions built over a plain predicate (or an expression tree), this makes `Matches` allocation-free. For propositions built over an underlying spec/policy whose predicate returns a `BooleanResult`/`PolicyResult`, `Matches` still produces those underlying per-model results (there is no boolean-only path through a caller-supplied result), but it avoids the higher-order wrapper allocation and short-circuits — so it allocates substantially less than `Evaluate`, not zero. `Evaluate` remains available when you need the full assertions and metadata.
