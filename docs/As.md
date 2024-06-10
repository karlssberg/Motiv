# `As()` - optional

This optional builder method creates higher-order propositions.
Higher-order propositions make statements about set of models.
In contrast, first-order propositions make statements about individual models.

Whilst you can also create higher-order propositions without using the `As()` method (by defining the 
model as a collection), at some point you will likely want to generate assertions using only the models that 
helped determine whether the higher-order proposition was satisfied (or not).

### All satisfied

### `.AsAllSatified()`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAllSatisfied()
    .Create("all are even")
```

The proposition is satisfied if all models in the collection are satisfied, otherwise it is not satisfied.

### Some satisfied

### `.AsAnySatisfied()`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAnySatisfied()
    .Create("some are even")
```

The proposition is satisfied if any of the models in the collection are satisfied, otherwise it is not satisfied.

### None satisfied

### `.AsNoneSatisfied()`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNoneSatisfied()
    .Create("none are even")
```

The proposition is satisfied if no models in the collection are satisfied, otherwise it is not satisfied.

### Minimum satisfied

### `.AsAtLeastNSatisfied(int n)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtLeastNSatisfied(3)
    .Create("3 or more are even")
```

The proposition is satisfied if at least `n` models in the collection are satisfied, otherwise it is not satisfied.

### Maximum satisfied

### `.AsAtMostNSatisfied(int n)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsAtMostNSatisfied(3)
    .Create("3 or fewer are even")
```

The proposition is satisfied if no more than `n` models in the collection are satisfied, otherwise it is not 
satisfied.

### N satisfied

### `.AsNSatisfied(int n)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .AsNSatisfied(3)
    .Create("3 are even")
```

The proposition is satisfied if `n` number of models in the collection are satisfied, otherwise it is not 
satisfied.

| [Back - _Build()_](./Build.md) | [Next - _WhenTrue()_](./WhenTrue.md) |
