# OrTogether()

```csharp
BooleanResultBase<TMetadata> OrTogether<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```

The `OrTogether()` extension method is used to combine a collection of boolean results into a single boolean result
that is satisfied if any of the original results are satisfied.  All results are evaluated, regardless of
whether the previous results have been satisfied.

```csharp
var specs = 
    [
        new IsEvenProposition(),
        new IsGreaterThanProposition(5)
    ];

var results = specs.Select(spec => spec.IsSatisfiedBy(6));

var orTogetherResult = results.OrTogether();

orTogetherResult.Reason;  // "is even & is greater than 5"
```