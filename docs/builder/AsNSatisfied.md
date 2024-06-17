### _n_ satisfied

```csharp
AsNSatisfied(int n)
```

The proposition is satisfied if `n` number of models in the collection are satisfied, otherwise it is not
satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNSatisfied(3)
    .Create("3 are even")
```