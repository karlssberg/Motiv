## Creating a composite specification

A composite specification is a specification that is composed of other specifications. The composite specification can
be created by using the `&` operator, which is the logical AND operator. The `&` operator is overloaded for
the `Spec<T>` class. The `&` operator returns a new `Spec<T>` instance that represents the logical AND of the two
specifications.

### Basic Specification

```csharp
SpecBase<int, string> isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue("the number is even")
    .WhenFalse("the number is odd")
    .CreateSpec();

isEven.IsSatisfiedBy(2).Satisfied; // returns true
isEven.IsSatisfiedBy(2).Reasons; // returns ["the number is even"]
isEven.IsSatisfiedBy(3).Reasons; // returns ["the number is odd"]
```

### Composite Specification
```csharp
SpecBase<int, string> isPositive = Spec
    .Build<int>(n => n > 0)
    .WhenTrue("the number is positive")
    .WhenFalse(n => n switch 
        {
            0 => "the number is zero",
            _ => "the number is negative"
        })
    .CreateSpec();

SpecBase<int, string> isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue("the number is even")
    .WhenFalse("the number is odd")
    .CreateSpec();

var isPositiveAndEven = isPositive & isEven;

isPositiveAndEven.IsSatisfiedBy(2).Satisfied; // returns true
isPositiveAndEven.IsSatisfiedBy(2).Reasons; // returns ["the number is even", "the number is positive"]

isPositiveAndEven.IsSatisfiedBy(3).Satisfied; // returns false
isPositiveAndEven.IsSatisfiedBy(3).Reasons; // returns ["the number is odd", "the number is positive"]

isPositiveAndEven.IsSatisfiedBy(-2).Satisfied; // returns false
isPositiveAndEven.IsSatisfiedBy(-2).Reasons; // returns ["the number is even", "the number is negative"]
```

### Custom type specification
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue(n => new { English = "the number is even", Spanish = "el número es par" })
    .WhenFalse(n => new { English = "the number is odd", Spanish = "el número es impar" })
    .CreateSpec("even number");

isEven.IsSatisfiedBy(2).Satisfied; // returns true
isEven.IsSatisfiedBy(2).Reasons; // returns ["even number is true"]
isEven.IsSatisfiedBy(2).Metadata.Select(m => m.English); // returns ["the number is even"]
isEven.IsSatisfiedBy(2).Metadata.Select(m => m.Spanish); // returns ["el número es par"]
```

