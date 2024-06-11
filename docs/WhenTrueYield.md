# WhenTrueYield()

The difference between `WhenTrueYield` and `WhenTrue` is that `WhenTrueYield` is used to generate multiple
assertions/metadata. This may be, for instance, because you want to pass through the underlying assertions instead of
summarizing them, or maybe you are working with higher-order propositions and want assertions for each unsatisfied
model in a set.

## Usage when building a new proposition

### Dynamic assertions (derived from model)

`.WhenTrueYield(Func<TModel, IEnumerable<string>> factory)`

```csharp
Spec.Build((string str) => str.Contains("foo") && str.Contains("bar"))
    .WhenTrueYield(str =>
        [
            $"'{str}' contains 'foo'",
            $"'{str}' contains 'bar'"
        ])
    .WhenFalse("does not contain 'foo' and 'bar'")
    .Create("contains 'foo' and 'bar'");
```

This overload generates multiple assertion statements based on the model when the proposition is satisfied. When the 
proposition is satisfied, the assertion result from the factory function will be used to populate the `Assertions` and 
`Metadata` properties of the result.

### Dynamic metadata (derived from model)

`.WhenTrueYield(Func<TModel, IEnumerable<TMetadata>> factory)`

```csharp
Spec.Build((string str) => str.Contains("foo") && str.Contains("bar"))
    .WhenTrueYield(str =>
        [
            new MyMetadata($"'{str}' contains 'foo'"),
            new MyMetadata($"'{str}' contains 'bar'")
        ])
    .WhenFalse(new MyMetadata("does not contain 'foo' and 'bar'"))
    .Create("contains 'foo' and 'bar'");
```

## Usage when building upon existing propositions or results

### Dynamic assertions (derived from model and underlying result)

`.WhenTrueYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrueYield((n, result) => result.Assertions)
    .WhenFalse("is odd")
    .Create("is even");
```

This is used to generate multiple assertion statements based on the model and the result of the underlying proposition.
When the proposition is satisfied, the assertion result from the factory function will be used to populate the
`Assertions` and `Metadata` properties of the result.

### Dynamic metadata (derived from model and underlying result)

`.WhenTrueYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrueYield((n, result) => result.Assertions.Select(assertion => new MyMetadata($"{n} {assertion}")))
    .WhenFalse(new MyMetadata("is odd"))
    .Create("is even");
```

This overload generates multiple metadata values based on the model and the result of the underlying proposition. When
the proposition is satisfied, the metadata values returned by the factory function will populate the `Metadata`
property of the result.

## Usage when building a higher-order proposition from an existing proposition

### Dynamic assertions (derived from pairwise model and result)

`.WhenTrueYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAnySatisfied()
    .WhenTrueYield(eval => $"{eval.TrueModels.Serialize()} are even"))
    .WhenFalse("is odd")
    .Create("all even");
```

<div style="display: flex; justify-content: space-between">
    <a href="./WhenTrue.md">&lt; Previous</a>
    <a href="./WhenFalse.md">Next &gt;</a>
</div>