# At least _n_ satisfied

```csharp
AsAtLeastNSatisfied(int n)
```

The proposition is satisfied if at least `n` models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtLeastNSatisfied(3)
    .Create("3 or more are even")
```