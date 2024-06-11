# WhenFalse()

### Fixed assertion

`.WhenFalse(string assertion)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")
    .WhenFalse("is odd")
    .Create();
```

This overload generates an assertion statement when the proposition is not satisfied. When the proposition is not
satisfied, the metadata returned by the factory function will be used to populate the
`Reason`, `Assertions` and `Metadata` properties of the result.

### Fixed metadata

`.WhenFalse(TMetadata metadata)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse(new MyMetadata("is odd"))
    .Create("is even");
```

This overload generates a metadata value when the proposition is not satisfied. When the proposition is not satisfied,
the metadata returned by the factory function will populate the `Metadata`
property of the result.

### Dynamic assertion (derived from model)

`.WhenFalse(Func<TModel, string> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")
    .WhenFalse(n => $"{n} is odd")
    .Create();
```

This overload generates an assertion statement based on the model when the proposition is not satisfied. When the
proposition is not satisfied, the metadata returned by the factory function will be used to populate the
`Reason`, `Assertions` and `Metadata` properties of the result.

### Dynamic metadata (derived from model)

`.WhenFalse(Func<TModel, TMetadata> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse(n => new MyMetadata($"{n} is odd"))
    .Create("is even");
```

This overload generates a metadata value based on the model when the proposition is not satisfied. When the proposition
is not satisfied, the metadata returned by the factory function will populate the `Metadata`
property of the result.

## Extra usages when building upon existing propositions or results

### Dynamic assertion (derived from model and underlying result)

`.WhenFalse(Func<TModel, BooleanResultBase<TMetadata>, string> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue("is even")
    .WhenFalse((n, result) => result.Assertions.Serialize())
    .Create();
```

This overload generates an assertion statement based on the model and the result of the underlying proposition. When the
proposition is not satisfied, the metadata returned by the factory function will be used to populate the
`Reason`, `Assertions` and `Metadata` properties of the result.

### Dynamic assertion (derived from model and underlying result)

`.WhenFalse(Func<TModel, BooleanResultBase<TMetadata>, TMetadata> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse((n, result) => new MyMetadata($"{n} {result.Reason}"))
    .Create("is even");
```

This overload generates a metadata value based on the model and the result of the underlying proposition. When the
proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata`
property of the result.

## Extra usages when building a higher-order proposition

### Dynamic assertion (derived from pairwise models and results)

`.WhenFalse(Func<HigherOrderBooleanEvaluation<TModel>, string> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAllSatisfied()
    .WhenTrue("is even")
    .WhenFalse(eval => $"{eval.CausalModels.Serialize()} are odd")
    .Create();
```

This overload generates an assertion statement based on the pairwise models and results when the proposition is not
satisfied. When the proposition is not satisfied, the metadata returned by the factory function will be used to populate
the `Reason`, `Assertions` and `Metadata` properties of the result.

### Dynamic metadata (derived from pairwise models and results)

`.WhenFalse(Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAllSatisfied()
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse(eval => new MyMetadata($"{eval.CausalModels.Serialize()} are odd"))
    .Create("is even");
```

This overload generates a metadata value based on the pairwise models and results when the proposition is not satisfied.
When the proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata`
property of the result.

## Extra usages when building a higher-order proposition from an existing proposition or result

### Dynamic assertion (derived from model and underlying result)

`.WhenFalse(Func<HigherOrderEvaluation<TModel>, string> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .AsAllSatisfied()
    .WhenTrue("is even")
    .WhenFalse(eval => eval.Assertions.Serialize())
    .Create();
```

This overload generates an assertion statement based on the model and the result of the underlying proposition when the
proposition is not satisfied. When the proposition is not satisfied, the metadata returned by the factory function will
be used to populate the `Reason`, `Assertions` and `Metadata` properties of the result.

### Dynamic metadata (derived from model and underlying result)

`.WhenFalse(Func<HigherOrderEvaluation<TModel>, TMetadata> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .AsAllSatisfied()
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse(eval => new MyMetadata(eval.Assertions.Serialize()))
    .Create("is even");
```

This overload generates a metadata value based on the model and the result of the underlying proposition when the
proposition is not satisfied. When the proposition is not satisfied, the metadata returned by the factory function will
populate the `Metadata` property of the result.

<div style="display: flex; justify-content: space-between">
    <a href="./WhenTrueYield.md">&lt; Previous</a>
    <a href="./WhenFalseYield.md">Next &gt;</a>
</div>