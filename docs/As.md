---
title: As()
category: building
---
This optional builder method creates [higher-order propositions](https://en.wikipedia.org/wiki/Higher-order_logic).
Higher-order propositions make statements about set of models.
In contrast, first-order propositions make statements about individual models.

You can create higher-order propositions without using the As() method by defining the model as a collection.
However, at some point, you will likely want to surface the underlying assertions that contributed to determining 
whether the higher-order proposition was satisfied or not.
The As() method (and its extensions) makes it possible to subsequently access these assertions.

| Method                                                  | Description                                                                                                |
|---------------------------------------------------------|------------------------------------------------------------------------------------------------------------|
| [As()](./As.html#custom-higher-order-propositions)      | Creates a custom higher-order proposition                                                                  |
| [AsAllSatisfied()](./As.html#all-satisfied)             | Creates a proposition that is satisfied when all <br/> models in the collection are satisfied              |
| [AsAnySatisfied()](./As.html#any-satisfied)             | Creates a proposition that is satisfied when any <br/> of the models in the collection are satisfied       |
| [AsNoneSatisfied()](./As.html#none-satisfied)           | Creates a proposition that is satisfied when none <br/>of the models in the collection are satisfied       |
| [AsAtLeastNSatisfied()](./As.html#at-least-n-satisfied) | Creates a proposition that is satisfied when at least `n` <br/>models in the collection are satisfied      |
| [AsAtMostNSatisfied()](./As.html#at-most-n-satisfied)   | Creates a proposition that is satisfied when no more than `n` <br/> models in the collection are satisfied |
| [AsNSatisfied()](./As.html#n-satisfied)                 | Creates a proposition that is satisfied when `n` <br/>number of models in the collection are satisfied          |


### Custom higher-order propositions
```csharp
As(Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate)
```

The proposition is satisfied when the custom higher order predicate is satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .As(results => results.CountTrue() == result.CountFalse())
    .Create("has equal amounts of odd and even")
```

### Custom higher-order propositions with causal result selection
```csharp
As(
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,  
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
```

The proposition is satisfied when the custom higher order predicate is satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .As(result => result.CountTrue() == result.CountFalse(),
        (_, results) => results)
    .Create("has equal amounts of odd and even")
```

### All satisfied

```csharp
AsAllSatified()
```

The proposition is satisfied if all models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAllSatisfied()
    .Create("all are even")
```

### Any satisfied

```csharp
AsAnySatisfied()
```

The proposition is satisfied if any of the models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAnySatisfied()
    .Create("some are even")
```

### None satisfied

```csharp
AsNoneSatisfied()
```

The proposition is satisfied if no models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNoneSatisfied()
    .Create("none are even")
```

### At least _n_ satisfied

```csharp
AsAtLeastNSatisfied(int n)
```

The proposition is satisfied if at least `n` models in the collection are satisfied, otherwise it is not satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtLeastNSatisfied(3)
    .Create("3 or more are even")
```

### At most _n_ satisfied

```csharp
AsAtMostNSatisfied(int n)
```

The proposition is satisfied if no more than `n` models in the collection are satisfied, otherwise it is not
satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtMostNSatisfied(3)
    .Create("3 or fewer are even")
```

### _n_ satisfied

```csharp
AsNSatisfied(int n)
```

The proposition is satisfied if `n` number of models in the collection are satisfied, otherwise it is not
satisfied.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNSatisfied(3)
    .Create("3 are even")
```

<div style="display: flex; justify-content: space-between">
    <a href="./Build.html">&lt; Previous</a>
    <a href="./WhenTrue.html">Next &gt;</a>
</div>