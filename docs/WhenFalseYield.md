# WhenFalseYield()

The difference between `WhenFalseYield` and `WhenFalse` is that `WhenFalseYield` is used to generate multiple 
assertions/metadata. This may be, for instance, because you want to pass through the underlying assertions instead of 
summarizing them, or maybe you are working with higher-order propositions and want assertions for each unsatisfied 
model in a set.

## Usage when building a new proposition

### Dynamic assertions (derived from model)

`.WhenFalseYield(Func<TModel, IEnumerable<string>> factory)`

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

`.WhenFalseYield(Func<TModel, IEnumerable<TMetadata>> factory)`

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

## Usage when building a higher-order proposition from an predicate function

When a predicate function is used by the `Build()` method, the factory function will receive a
`HigherOrderBooleanEvaluation<TModel>` containing the pairwise models and results (`BooleanResult<TModel, TMetadata>`)
of the proposition.

### Dynamic assertions (derived from pairwise model and result)

`.WhenFalseYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAllSatisfied()
    .WhenTrue("all are even")
    .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is odd"))
    .Create();
```

This overload gives you access to the models (and various aspects of them) so that you can generate multiple 
distinct assertion statements.

### Dynamic metadata (derived from pairwise model and result)

`.WhenFalseYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAllSatisfied()
    .WhenTrueYield(new MyMetadata("all are even"))
    .WhenFalse(eval => eval.TrueModels.Select(n => new MyMetadata($"{n} is odd")))
    .Create("all even");
```

This overload gives you access to the models (and various aspects of them) so that you can generate multiple distinct 
metadata values.

## Usage when building a higher-order proposition from an existing proposition

When an existing proposition is used by the `Build()` method, the factory function will receive have access to the 
models and information about whether they are satisfied or not.

### Dynamic assertions (derived from pairwise model and result)

`.WhenFalseYield(Func<HigherOrderEvaluation<TModel>, IEnumerable<string>> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .AsAllSatisfied()
    .WhenTrue("all are even")
    .WhenFlaseYield(eval => eval.TrueModels.Select(n => $"{n} is odd"))
    .Create();
```

This overload gives you access to the models and their results so that you can generate multiple distinct assertion
statements.

### Dynamic metadata (derived from pairwise model and result)

`.WhenFalseYield(Func<HigherOrderEvaluation<TModel>, IEnumerable<TMetadata>> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .AsAllSatisfied()
    .WhenTrue(new MyMetadata("all are even"))
    .WhenFlaseYield(eval => eval.TrueModels.Select(n => new MyMetadata($"{n} is odd")))
    .Create("all even");
```

This overload gives you access to the models and their results so that you can generate multiple distinct metadata 
objects.

<div style="display: flex; justify-content: space-between">
    <a href="./WhenFalse.html">&lt; Previous</a>
    <a href="./Create.html">Next &gt;</a>
</div>