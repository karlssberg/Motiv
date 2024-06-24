# AndTogether()

```csharp
BooleanResultBase<TMetadata> AndTogether<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `AndTogether()` extension method is used to combine a collection of boolean results into a single boolean result
that is satisfied if all the original results are satisfied.  All results are evaluated, regardless of
whether the previous results have been satisfied.

```csharp
var specs = 
    [
        new IsEvenProposition(),
        new IsGreaterThanProposition(5)
    ];

var results = specs.Select(spec => spec.IsSatisfiedBy(6));

var andTogetherResult = results.AndTogether();

andTogetherResult.Reason;  // "is even & is greater than 5"
```