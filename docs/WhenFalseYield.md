# WhenFalseYield()

The difference between `WhenFalseYield` and `WhenFalse` is that `WhenFalseYield` is used to generate multiple 
assertions/metadata. This may be, for instance, because you want to pass through the underlying assertions instead of 
summarizing them, or maybe you are working with higher-order propositions and want assertions for each unsatisfied 
model in a set.

## Usage when building a new proposition

### Dynamic assertions (derived from model)

`.WhenTrueYield(Func<TModel, IEnumerable<string>> factory)`

```csharp
Spec.Build((string str) => str.Contains("foo") || str.Contains("bar"))
    .WhenTrue("contains 'foo' or 'bar'")
    .WhenFalseYield(str =>
        [
            $"'{str}' does not contain 'foo'",
            $"'{str}' does not contain 'bar'"
        ])
    .Create();
```

This overload generates multiple assertion statements based on the model when the proposition is satisfied. When the
proposition is satisfied, the assertion result from the factory function will be used to populate the `Assertions` and
`Metadata` properties of the result.

### Dynamic metadata (derived from model)

`.WhenTrueYield(Func<TModel, IEnumerable<TMetadata>> factory)`

```csharp
Spec.Build((string str) => str.Contains("foo") || str.Contains("bar"))
    .WhenTrue(new MyMetadata("contains 'foo' or 'bar'"))
    .WhenFalseYield(str =>
        [
            new MyMetadata($"'{str}' does not contain 'foo'"),
            new MyMetadata($"'{str}' does not contains 'bar'")
        ])
    .Create();
```

### Dynamic assertions (derived from model and underlying result)

`.WhenFalseYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> factory)`

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

`.WhenFalseYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalseYield((n, result) => result.Assertions.Select(assertion => new MyMetadata($"{n} {assertion}")))
    .Create("is even");
```

This overload generates multiple metadata values based on the model and the result of the underlying proposition. When
the proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata`
property of the result.

## Usage when building a higher-order proposition from an existing proposition

### Dynamic assertions (derived from pairwise model and result)

`.WhenTrueYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAnySatisfied()
    .WhenTrue("is even")
    .WhenFalseYield(eval => $"{eval.FalseModels.Serialize()} are odd"))
    .Create();
```

<div style="display: flex; justify-content: space-between">
    <a href="./WhenFalse.md">&lt; Previous</a>
    <a href="./Create.md">Next &gt;</a>
</div>