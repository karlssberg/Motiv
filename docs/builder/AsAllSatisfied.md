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

When you only need the boolean outcome, call `Matches(models)` instead of `Evaluate(models)`. For the built-in higher-order operations (`AsAllSatisfied`, `AsAnySatisfied`, `AsNoneSatisfied`, `AsAtLeastNSatisfied`, `AsAtMostNSatisfied`, `AsNSatisfied`), `Matches` short-circuits — it stops evaluating models as soon as the outcome is decided — and avoids allocating the per-model result objects that `Evaluate` builds. `Evaluate` remains available when you need the full assertions and metadata.
