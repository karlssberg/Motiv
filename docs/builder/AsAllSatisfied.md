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
