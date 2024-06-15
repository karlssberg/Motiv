---
title: WhenFalseYield()
category: building
---
The difference between `WhenFalseYield` and `WhenFalse` is that `WhenFalseYield` is used to generate multiple 
assertions/metadata. This may be, for instance, because you want to pass through the underlying assertions instead of 
summarizing them, or maybe you are working with higher-order propositions and want assertions for each unsatisfied 
model in a set.

## Factory Functions

Different overloads are made available for different use cases—depending on which `Build()` overload was previously
chosen.

### New propositions

| Type                                   | Description                                                    |
|----------------------------------------|----------------------------------------------------------------|
| `Func<TModel, IEnumerable<string>>`    | a factory function that returns multiple assertion statements. |
| `Func<TModel, IEnumerable<TMetadata>>` | a factory function that returns multiple metadata values.      |

### Reusing existing propositions or their results

| Type                                                                 | Description                                                    |
|----------------------------------------------------------------------|----------------------------------------------------------------|
| `Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>>`    | a factory function that returns multiple assertion statements. |
| `Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>>` | a factory function that returns multiple metadata values.      |

### Higher-order propositions from a predicate function

| Type                                                                 | Description                                                    |
|----------------------------------------------------------------------|----------------------------------------------------------------|
| `Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>>`    | a factory function that returns multiple assertion statements. |
| `Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>>` | a factory function that returns multiple metadata values.      |

### Higher-order propositions from an existing proposition

| Type                                                                     | Description                                                    |
|--------------------------------------------------------------------------|----------------------------------------------------------------|
| `Func<HigherOrderEvaluation<TModel>, IEnumerable<string>>`               | a factory function that returns multiple assertion statements. |
| `Func<HigherOrderEvaluation<TModel, TMetadata>, IEnumerable<TMetadata>>` | a factory function that returns multiple metadata values.      |

## Usage when building a new proposition

When the `Build()` method is called using a predicate function, or an existing proposition, you can use the model to
generate multiple assertions or metadata values from it.

### Dynamic assertions (derived from model)

```csharp
WhenFalseYield(Func<TModel, IEnumerable<string>> factory)
```

This overload generates multiple assertion statements based on the model when the proposition is satisfied.

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

### Dynamic metadata (derived from model)

```csharp
WhenFalseYield(Func<TModel, IEnumerable<TMetadata>> factory)
```

This overload generates multiple metadata values based on the model when the proposition is not satisfied.

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
## Usage when building upon existing propositions or results

Existing propositions can be re-used to build new propositions.
In this case, the factory function will receive the model and the result of the underlying proposition so that you
can generate multiple assertions or metadata values from them.

### Dynamic assertions (derived from model and underlying result)

```csharp
WhenFalseYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> factory)
```


This is used to generate multiple assertion statements based on the model and the result of the underlying proposition.
```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue("is even")
    .WhenFalseYield((n, result) => result.Assertions)
    .Create();
```

### Dynamic metadata (derived from model and underlying result)

```csharp
WhenFalseYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> factory)
```

This overload generates multiple metadata values based on the model and the result of the underlying proposition.

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalseYield((n, result) => result.Assertions.Select(assertion => new MyMetadata($"{n} {assertion}")))
    .Create("is even");
```

## Usage when building a higher-order proposition from an predicate function

When a predicate function is used by the `Build()` method, the factory function will receive a
`HigherOrderBooleanEvaluation<TModel>` containing the models and their results.

### Dynamic assertions (derived from pairwise model and result)

```csharp
WhenFalseYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> factory)
```

This overload gives you access to the models (and various aspects of them) so that you can generate multiple
distinct assertion statements.

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAllSatisfied()
    .WhenTrue("all are even")
    .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is odd"))
    .Create();
```

### Dynamic metadata (derived from pairwise model and result)

```csharp
WhenFalseYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> factory)
```

This overload gives you access to the models (and various aspects of them) so that you can generate multiple distinct
metadata values.

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAllSatisfied()
    .WhenTrueYield(new MyMetadata("all are even"))
    .WhenFalse(eval => eval.TrueModels.Select(n => new MyMetadata($"{n} is odd")))
    .Create("all even");
```

## Usage when building a higher-order proposition from an existing proposition


When an existing proposition is used by the `Build()` method, the factory function will receive a
`HigherOrderEvaluation<TModel, TMetadata>` containing the models and their results.

### Dynamic assertions (derived from pairwise model and result)

```csharp
WhenFalseYield(Func<HigherOrderEvaluation<TModel, TMetadata>, IEnumerable<string>> factory)
```

This overload gives you access to the models and their results so that you can generate multiple distinct assertion
statements.

```csharp
Spec.Build(new IsEvenProposition())
    .AsAllSatisfied()
    .WhenTrue("all are even")
    .WhenFlaseYield(eval => eval.TrueModels.Select(n => $"{n} is odd"))
    .Create();
```

### Dynamic metadata (derived from pairwise model and result)

```csharp
WhenFalseYield(Func<HigherOrderEvaluation<TModel, TMetadata>, IEnumerable<TMetadata>> factory)
```

This overload gives you access to the models and their results so that you can generate multiple distinct metadata
objects.

```csharp
Spec.Build(new IsEvenProposition())
    .AsAllSatisfied()
    .WhenTrue(new MyMetadata("all are even"))
    .WhenFlaseYield(eval => eval.TrueModels.Select(n => new MyMetadata($"{n} is odd")))
    .Create("all even");
```

<div style="display: flex; justify-content: space-between">
    <a href="./WhenFalse.html">&lt; Previous</a>
    <a href="./Create.html">Next &gt;</a>
</div>