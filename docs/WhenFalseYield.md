# WhenFalseYield()

The difference between `WhenFalseYield` and `WhenFalse` is that `WhenFalseYield` is used to generate multiple 
assertions/metadata. This may be, for instance, because you want to pass through the underlying assertions instead of 
summarizing them, or maybe you are working with higher-order propositions and want assertions for each unsatisfied 
model in a set.

### Dynamic assertions (derived from model and underlying result)

### `.WhenFalseYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> factory)`
`factory` - A factory function that returns multiple assertions.
Both the model and the boolean result of the underlying proposition are passed as arguments to the factory function.

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue("is even")
    .WhenFalseYield((n, result) => result.Assertions)
    .Create();
```

This overload generates multiple assertion statements based on the model and the result of the underlying proposition.
When the proposition is not satisfied, the metadata returned by the factory function will be used to populate the
`Reason`, `Assertions` and `Metadata` properties of the result.

### Dynamic metadata (derived from model and underlying result)

### `.WhenFalseYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalseYield((n, result) => result.Assertions.Select(assertion => new MyMetadata($"{n} {assertion}")))
    .Create("is even");
```

This overload generates multiple metadata values based on the model and the result of the underlying proposition. When
the proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata`
property of the result.

| [Back - _WhenFalse()_](./WhenFalse.md) | [Next - _Create()_](./Create.md) |
|:--------------------------------------:|:--------------------------------:|