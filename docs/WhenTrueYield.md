---
title: WhenTrueYield()
category: building
---
The difference between `WhenTrueYield` and `WhenTrue` is that `WhenTrueYield` is used to generate multiple
assertions/metadata. This may be, for instance, because you want to pass through the underlying assertions instead of
summarizing them, or maybe you are working with higher-order propositions and want assertions for each unsatisfied
model in a set.

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
WhenTrueYield(Func<TModel, IEnumerable<string>> factory)
```

This overload generates multiple assertion statements based on the model when the proposition is satisfied.

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

### Dynamic metadata (derived from model)

```csharp
WhenTrueYield(Func<TModel, IEnumerable<TMetadata>> factory)
```

This overload generates multiple metadata instances based on the model when the proposition is satisfied.

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

Existing propositions can be re-used to build new propositions.
In this case, the factory function will receive the model and the result of the underlying proposition so that you 
can generate multiple assertions or metadata values from them.

### Dynamic assertions (derived from model and underlying result)

```csharp
WhenTrueYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> factory)
```

This is used to generate multiple assertion statements based on the model and the result of the underlying proposition.

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrueYield((n, result) => result.Assertions)
    .WhenFalse("is odd")
    .Create("is even");
```

### Dynamic metadata (derived from model and underlying result)

```csharp
WhenTrueYield(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> factory)
```

This overload generates multiple metadata values based on the model and the result of the underlying proposition.

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrueYield((n, result) => result.Assertions.Select(assertion => new MyMetadata($"{n} {assertion}")))
    .WhenFalse(new MyMetadata("is odd"))
    .Create("is even");
```

## Usage when building a higher-order proposition from a predicate function

When a predicate function is used by the `Build()` method, the factory function will receive a
`HigherOrderBooleanEvaluation<TModel>` containing the models and their results.

### Dynamic assertions (derived from pairwise model and result)

```csharp
WhenTrueYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> factory)
```

This overload gives you access to the models (and various aspects of them) so that you can generate multiple
distinct assertion statements.

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAnySatisfied()
    .WhenTrueYield(eval => eval.TrueModels.Select(n => $"{n} is even"))
    .WhenFalse("all are odd")
    .Create("all even");
```

### Dynamic metadata (derived from pairwise model and result)

```csharp
WhenTrueYield(Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> factory)
```

This overload gives you access to the models (and various aspects of them) so that you can generate multiple distinct
metadata values.

```csharp
Spec.Build((int n) => n % 2 == 0))
    .AsAnySatisfied()
    .WhenTrueYield(eval => eval.TrueModels.Select(n => new MyMetadata($"{n} is even")))
    .WhenFalse(new MyMetadata("all are odd"))
    .Create("all even");
```

## Usage when building a higher-order proposition from an existing proposition

When an existing proposition is used by the `Build()` method, the factory function will receive a
`HigherOrderEvaluation<TModel, TMetadata>` containing the models and their results.

### Dynamic assertions (derived from pairwise model and result)

```csharp
WhenTrueYield(Func<HigherOrderEvaluation<TModel, TMetadata>, IEnumerable<string>> factory)
```

This overload gives you access to the models and their results so that you can generate multiple distinct assertion
statements.

```csharp
Spec.Build(new IsEvenProposition())
    .AsAnySatisfied()
    .WhenTrueYield(eval => eval.TrueModels.Select(n => $"{n} is even"))
    .WhenFalse("all are odd")
    .Create("all even");
```

### Dynamic metadata (derived from pairwise model and result)

```csharp
WhenTrueYield(Func<HigherOrderEvaluation<TModel, TMetadata>, IEnumerable<TMetadata>> factory)
```

This overload gives you access to the models and their results so that you can generate multiple distinct metadata
objects.

```csharp
Spec.Build(new IsEvenProposition())
    .AsAnySatisfied()
    .WhenTrueYield(eval => eval.TrueModels.Select(n => new MyMetadata($"{n} is even")))
    .WhenFalse(new MyMetadata("all are odd"))
    .Create("all even");
```

<div style="display: flex; justify-content: space-between">
    <a href="./WhenTrue.html">&lt; Previous</a>
    <a href="./WhenFalse.html">Next &gt;</a>
</div>