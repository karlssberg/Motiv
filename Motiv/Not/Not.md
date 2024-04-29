# Logical NOT `!`
A logical NOT operation can be performed on a specification using the `!` operator `!spec`,
or alternatively using the `Not` method `spec.Not()`.
This will produce a new specification instance that is the logical NOT of the original specification.

For example:

```csharp

var isEvenSpec = Spec
    .Build((int n) => n % 2 == 0)
    .WhenTrue("even")
    .WhenFalse("odd")
    .Create();

var isOddSpec = !isEvenSpec; // same as: isEvenSpec.Not()

var isOdd = isOddSpec.IsSatisfiedBy(3);
isOdd.Satisfied; // true
isOdd.Reason; // "odd"
isOdd.Assertions; // ["odd"]
```

