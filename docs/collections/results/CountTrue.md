# CountTrue()

```csharp
int CountFalse<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `CountTrue()` extension method is used to count the number of boolean results that are satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().Evaluate(4),
        new IsGreaterThanProposition(5).Evaluate(4)
    ];

results.CountTrue();  // 1
```