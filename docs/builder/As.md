---
title: As()
category: building
---
# As()

This optional builder method creates [higher-order propositions](https://en.wikipedia.org/wiki/Higher-order_logic).
Higher-order propositions make statements about set of models.
In contrast, first-order propositions make statements about individual models.

You can create higher-order propositions without using the As() method by defining the model as a collection.
However, at some point, you will likely want to surface the underlying assertions that contributed to determining 
whether the higher-order proposition was satisfied or not.
The As() method (and its extensions) makes it possible to subsequently access these assertions.


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
