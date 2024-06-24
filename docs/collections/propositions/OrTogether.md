# OrTogether()

```csharp
SpecBase<TModel, TMetadata> OrTogether<TModel, TMetadata>(
    this IEnumerable<SpecBase<TModel, TMetadata>> specs)
```

The `OrTogether()` extension method is used to combine a collection of specifications into a single specification
that is satisfied if any of the original specifications are satisfied.  All specifications are evaluated, regardless of
whether the previous specifications have been satisfied.

```csharp
IEnumerable<SpecBase<int>> specs = 
    [
        new IsEvenProposition(),
        new IsGreaterThanProposition(5)
    ];

var isEvenOrGreaterThanFive = specs.OrTogether();

isEvenOrGreaterThanFive.IsSatisfiedBy(6).Reason;  // "is even | is greater than 5"
isEvenOrGreaterThanFive.IsSatisfiedBy(3).Reason;  // "is odd | is less than or equal to 5"
```