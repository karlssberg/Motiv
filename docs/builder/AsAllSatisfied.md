# All satisfied

```csharp
AsAllSatified()
```

The proposition is satisfied if all models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAllSatisfied()
    .Create("all are even")
```