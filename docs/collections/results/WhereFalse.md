# WhereFalse()

```csharp
IEnumerable<BooleanResultBase<TMetadata>> WhereFalse<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `WhereFalse()` extension method is used to filter a collection of boolean results based on whether the result is
not satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().IsSatisfiedBy(4),
        new IsGreaterThanProposition(5).IsSatisfiedBy(4)
    ];

var falseResults = results.WhereFalse();

falseResults.Select(result => result.Reason);  // [ "is greater than 5" ]
```