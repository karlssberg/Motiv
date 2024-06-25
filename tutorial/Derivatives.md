# Derivatives

In the same way that a regular predicate function (`Func<TModel, bool>`) can be used to create a proposition, a
proposition can also be used to create a new proposition.
This is done by passing an existing proposition to the fluent `Spec.Build()` method.

## Simple Encapsulation

Existing propositions can have their explanations/metadata changed by encapsulating them in a new proposition.

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue("is even")
    .WhenFalse("is not even")
    .Create();
```

## Factory Method
Sometimes you may want to have fine-grained explanations, which typically entails defining additional propositions.
To help ensure the underlying propositions are kept together, a factory method can be used to create the proposition.

For example:
```csharp
var isEvenAndPositive =
    Spec.Build(() =>
            {
                var isEven =
                    Spec.Build(new IsEvenProposition())
                        .WhenTrue("even")
                        .WhenFalse("not even")
                        .Create();

                var isPositive =
                    Spec.Build(new IsPositiveProposition())
                        .WhenTrue("positive")
                        .WhenFalse("not positive")
                        .Create();

                return isEven & isPositive; // boolean composition
            })
        .WhenTrue("is even and positive")
        .WhenFalse((n, result) => $"{n} is {result.Assertions.Serialize()}"))
        .Create();

isEvenAndPositive.IsSatisfiedBy(2).Reason;  // "is even and positive"
isEvenAndPositive.IsSatisfiedBy(-3).Reason; // "-3 is not even and not positive"
isEvenAndPositive.IsSatisfiedBy(-2).Reason; // "-2 is not positive"
```
