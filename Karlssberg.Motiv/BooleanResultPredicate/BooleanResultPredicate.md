# BooleanResult Predicate
A BooleanResult predicate is a predicate that returns a `BooleanResult` instance, instead of a `bool` value.
Semantically they perform the same function, but by returning a `BooleanResult` instance.
This if for when you want to evaluate specifications within a predicate, but don't want to lose 
underlying explanations or metadata.
```csharp
var isLongEvenSpec = 
    Spec.Build((long n) => n % 2 == 0)
        .WhenTrue("even")
        .WhenFalse("odd")
        .Create();

var isDecimalPositiveSpec =
    Spec.Build((decimal n) => n > 0)
        .WhenTrue("positive")
        .WhenFalse("not positive")
        .Create();

var isIntegerPositiveAndEvenSpec = 
    Spec.Build((int n) => isLongEvenSpec.IsSatisfiedBy(n) & isDecimalPositiveSpec.IsSatisfiedBy(n))
        .Create("even and positive");

isIntegerPositiveAndEvenSpec.IsSatisfiedBy(2).AllRootAssertions;  // ["even", "positive"]
isIntegerPositiveAndEvenSpec.IsSatisfiedBy(3).AllRootAssertions;  // ["odd", "positive"]
isIntegerPositiveAndEvenSpec.IsSatisfiedBy(0).AllRootAssertions;  // ["even", "not positive"]
isIntegerPositiveAndEvenSpec.IsSatisfiedBy(-3).AllRootAssertions; // ["odd", "not positive"]
```
