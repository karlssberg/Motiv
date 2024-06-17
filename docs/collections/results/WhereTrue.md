#  WhereTrue()

```csharp
IEnumerable<BooleanResultBase<TMetadata>> WhereTrue<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `WhereTrue()` extension method is used to filter a collection of boolean results based on whether the result is
satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().IsSatisfiedBy(4),
        new IsGreaterThanProposition(5).IsSatisfiedBy(4)
    ];

var trueResults = results.WhereTrue();

trueResults.Select(result => result.Reason);  // [ "is even" ]
```