# AndTogether()

```csharp
SpecBase<TModel, TMetadata> AndTogether<TModel, TMetadata>(
    this IEnumerable<SpecBase<TModel, TMetadata>> specs)
```

The `AndTogether()` extension method is used to combine a collection of specifications into a single specification
that is satisfied if all the original specifications are satisfied.  All specifications are evaluated, regardless of
whether the previous specifications have been satisfied.

```csharp
IEnumerable<SpecBase<int>> specs = 
    [
        new IsEvenProposition(),
        new IsGreaterThanProposition(5)
    ];

var isEvenAndGreaterThanFive = specs.AndTogether();

isEvenAndGreaterThanFive.Evaluate(6).Reason;  // "is even & is greater than 5"
isEvenAndGreaterThanFive.Evaluate(3).Reason;  // "is odd & is less than or equal to 5"
```