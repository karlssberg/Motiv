### None satisfied

```csharp
AsNoneSatisfied()
```

The proposition is satisfied if no models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNoneSatisfied()
    .Create("none are even")
```