# BooleanResult Predicate
A BooleanResult predicate is a predicate that returns a `BooleanResult` instance, instead of a `bool` value.
Semantically they perform the same function, but by returning a `BooleanResult` instance.
This if for when you want to evaluate specifications within a predicate, but don't want to lose 
underlying explanations or metadata.
```csharp
var isEvenSpec = 
    Spec.Build((long n) => n % 2 == 0)
        .WhenTrue("even")
        .WhenFalse("odd")
        .Create();

var isPositiveSpec =
    Spec.Build((decimal n) => n > 0)
        .WhenTrue("positive")
        .WhenFalse("not positive")
        .Create();

var isEvenAndPositiveSpec = 
    Spec.Build((int n) => isEvenSpec.IsSatisfiedBy(n) & isPositiveSpec.IsSatisfiedBy(n))
        .Create("even and positive");

isEvenAndPositiveSpec.IsSatisfiedBy(2).AllRootAssertions;  // ["even", "positive"]
isEvenAndPositiveSpec.IsSatisfiedBy(3).AllRootAssertions;  // ["odd", "positive"]
isEvenAndPositiveSpec.IsSatisfiedBy(0).AllRootAssertions;  // ["even", "not positive"]
isEvenAndPositiveSpec.IsSatisfiedBy(-3).AllRootAssertions; // ["odd", "not positive"]
```
