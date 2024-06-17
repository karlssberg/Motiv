# CountFalse()

```csharp
int CountFalse<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `CountFalse()` extension method is used to count the number of boolean results that are not satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().IsSatisfiedBy(4),
        new IsGreaterThanProposition(5).IsSatisfiedBy(4)
    ];

results.CountFalse();  // 1
```