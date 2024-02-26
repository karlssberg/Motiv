## Creating a composite specification

A composite specification is a specification that is composed of other specifications. The composite specification can
be created by using the `&` operator, which is the logical AND operator. The `&` operator is overloaded for
the `Spec<T>` class. The `&` operator returns a new `Spec<T>` instance that represents the logical AND of the two
specifications.

### Basic Specification

At its most basic you can provide a predicate and a propositional statement.
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .CreateSpec("even");

isEven.IsSatisfiedBy(2).Satisfied; // returns true
isEven.IsSatisfiedBy(3).Satisfied; // returns false
isEven.IsSatisfiedBy(2).Reasons; // returns ["even"]
isEven.IsSatisfiedBy(3).Reasons; // returns ["!even"]
```

However, the real power of the library comes from the ability to provide a reason for when either the result is true or false.

```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue("number is even")
    .WhenFalse("number is odd")
    .CreateSpec();

isEven.IsSatisfiedBy(2).Reasons; // returns ["number is even"]
isEven.IsSatisfiedBy(3).Reasons; // returns ["number is odd"]
```

You can also provide a function that returns a description based on the value of the input.
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue(n => $"{n} is even")
    .WhenFalse(n => $"{n} is odd")
    .CreateSpec();

isEven.IsSatisfiedBy(2).Reasons; // returns ["2 is even"]
isEven.IsSatisfiedBy(3).Reasons; // returns ["3 is odd"]
```


### Composite Specification
```csharp
var isPositive = Spec
    .Build<int>(n => n > 0)
    .WhenTrue("the number is positive")
    .WhenFalse(n => $"the number is {n < 0 ? "negative" : "zero"}")
    .CreateSpec();

var isEven = Spec
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

