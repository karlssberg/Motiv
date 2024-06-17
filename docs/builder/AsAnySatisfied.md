# Any satisfied

```csharp
AsAnySatisfied()
```

The proposition is satisfied if any of the models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAnySatisfied()
    .Create("some are even")
```