# `As()` (optional)

This optional builder method creates [higher-order propositions](https://en.wikipedia.org/wiki/Higher-order_logic).
Higher-order propositions make statements about set of models.
In contrast, first-order propositions make statements about individual models.

You can create higher-order propositions without using the As() method by defining the model as a collection.
However, at some point, you will likely want to surface the underlying assertions that contributed to determining 
whether the higher-order proposition was satisfied or not.
The As() method (and its extensions) makes it possible to subsequently access these assertions.

### Custom higher-order propositions
`.As(Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .As(results => results.CountTrue() == result.CountFalse())
    .Create("has equal amounts of odd and even")
```
The proposition is satisfied when the custom higher order predicate is satisfied, otherwise it is not satisfied.

### Custom higher-order propositions with causal result selection
`.As(Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,  Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .As(result => result.CountTrue() == result.CountFalse(),
        (_, results) => results)
    .Create("has equal amounts of odd and even")
```
The proposition is satisfied when the custom higher order predicate is satisfied, otherwise it is not satisfied.

### All satisfied

`.AsAllSatified()`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAllSatisfied()
    .Create("all are even")
```

The proposition is satisfied if all models in the collection are satisfied, otherwise it is not satisfied.

### Some satisfied

`.AsAnySatisfied()`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAnySatisfied()
    .Create("some are even")
```

The proposition is satisfied if any of the models in the collection are satisfied, otherwise it is not satisfied.

### None satisfied

`.AsNoneSatisfied()`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNoneSatisfied()
    .Create("none are even")
```

The proposition is satisfied if no models in the collection are satisfied, otherwise it is not satisfied.

### Minimum satisfied

`.AsAtLeastNSatisfied(int n)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtLeastNSatisfied(3)
    .Create("3 or more are even")
```

The proposition is satisfied if at least `n` models in the collection are satisfied, otherwise it is not satisfied.

### Maximum satisfied

`.AsAtMostNSatisfied(int n)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtMostNSatisfied(3)
    .Create("3 or fewer are even")
```

The proposition is satisfied if no more than `n` models in the collection are satisfied, otherwise it is not 
satisfied.

### N satisfied

`.AsNSatisfied(int n)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNSatisfied(3)
    .Create("3 are even")
```

The proposition is satisfied if `n` number of models in the collection are satisfied, otherwise it is not 
satisfied.

<div style="display: flex; justify-content: space-between">
    <a href="./Build.md">&lt; Previous</a>
    <a href="./WhenTrue.md">Next &gt;</a>
</div>