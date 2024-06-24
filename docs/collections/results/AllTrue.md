# AllTrue()

```csharp
bool AllTrue<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `AllTrue()` extension method is used to determine whether all the boolean results are satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().IsSatisfiedBy(6),
        new IsGreaterThanProposition(5).IsSatisfiedBy(6)
    ];

results.AllTrue();  // true
```