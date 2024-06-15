---
title: WhenFalse()
category: building
---
The `WhenFalse()` method is used to yield values when the proposition is unsatisfied.
It also implicitly sets `TMetadata` type for the rest of the proposition.
Any values yielded from underlying propositions (if they exist) will be supplanted by the new value from the 
`WhenFalse()` method.
However, should the underlying yielded values still be required, then it is still possible to re-yield them.

Whilst the `WhenFalse()` method overloads remains broadly consistent across the various ways a proposition can be built,
there are nuances to be aware of that are as a result of prior builder method calls.
The outliers are the Higher-Order propositions, which require the pairing of models and results to be preserved for 
them to be useful.

This method is overloaded and takes one of the following types of arguments:
* `string` - a fixed assertion statement.
* `TMetadata` - a fixed metadata value.
* `Func<TModel, string>` - a factory function that returns an assertion statement.
* `Func<TModel, TMetadata>` - a factory function that returns a metadata value.
* `Func<TModel, BooleanResultBase<TMetadata>, string>` - a factory function that returns an assertion statement.
* `Func<TModel, BooleanResultBase<TMetadata>, TMetadata>` - a factory function that returns a metadata value.

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
    <a href="./WhenTrueYield.html">&lt; Previous</a>
    <a href="./WhenFalseYield.html">Next &gt;</a>
</div>