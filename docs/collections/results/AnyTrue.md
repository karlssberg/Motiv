# AnyTrue()

```csharp
bool AnyTrue<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `AnyTrue()` extension method is used to determine whether any of the boolean results are satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().IsSatisfiedBy(6),
        new IsGreaterThanProposition(5).IsSatisfiedBy(1)
    ];

results.AnyTrue();  // true
```