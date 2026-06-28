# AnyFalse()

```csharp
bool AnyFalse<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `AnyFalse()` extension method is used to determine whether any of the boolean results are not satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().Evaluate(1),
        new IsGreaterThanProposition(5).Evaluate(6)
    ];

results.AnyFalse();  // true
```