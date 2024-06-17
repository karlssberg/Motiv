# At most _n_ satisfied

```csharp
AsAtMostNSatisfied(int n)
```

The proposition is satisfied if no more than `n` models in the collection are satisfied, otherwise it is not
satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtMostNSatisfied(3)
    .Create("3 or fewer are even")
```