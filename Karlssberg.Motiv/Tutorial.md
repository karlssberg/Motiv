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
    .Create("is even");

isEven.IsSatisfiedBy(2).Satisfied; // true
isEven.IsSatisfiedBy(2).Reason; // "is even"
isEven.IsSatisfiedBy(2).Assertions; // ["is even"]

isEven.IsSatisfiedBy(3).Satisfied; // false
isEven.IsSatisfiedBy(3).Reason; // "!is even"
isEven.IsSatisfiedBy(3).Assertions; // ["!is even"]
```

However, the real power of the library comes from the ability to provide a reason for when either the result is true or
false.

```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue("number is even")
    .WhenFalse("number is odd")
    .Create();

isEven.IsSatisfiedBy(2).Reason; // "number is even"
isEven.IsSatisfiedBy(2).Assertions; // ["number is even"]

isEven.IsSatisfiedBy(3).Reason; // "number is odd"
isEven.IsSatisfiedBy(3).Assertions; // ["number is odd"]
```

You can also provide a function that returns a description based on the value of the input.

```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue(n => $"{n} is even")
    .WhenFalse(n => $"{n} is odd")
    .Create("is even");

isEven.IsSatisfiedBy(2).Reason; // "is even"
isEven.IsSatisfiedBy(2).Assertions; // ["2 is even"]

isEven.IsSatisfiedBy(3).Reason; // "!is even"
isEven.IsSatisfiedBy(3).Assertions; // ["3 is odd"]
```

### Handling multiple languages

If you want to present the text to an international audience, you can provide a custom object for `.WhenTrue()` and
`.WhenFalse()` instead of using a string.

```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue(n => new { English = "the number is even", Spanish = "el número es par" })
    .WhenFalse(n => new { English = "the number is odd", Spanish = "el número es impar" })
    .Create("is even number");

isEven.IsSatisfiedBy(2).Satisfied; // true
isEven.IsSatisfiedBy(2).Reason; // "is even number"
isEven.IsSatisfiedBy(2).Metadata.Select(m => m.English); // ["the number is even"]
isEven.IsSatisfiedBy(2).Metadata.Select(m => m.Spanish); // ["el número es par"]
```
### Composite Specification
```csharp
var isPositive = Spec
    .Build<int>(n => n > 0)
    .WhenTrue("the number is positive")
    .WhenFalse(n => $"the number is {n < 0 ? "negative" : "zero"}")
    .Create();

var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue("the number is even")
    .WhenFalse("the number is odd")
    .Create(); 

var isPositiveAndEven = isPositive & isEven;

isPositiveAndEven.IsSatisfiedBy(2).Satisfied; // true
isPositiveAndEven.IsSatisfiedBy(2).Reason; // "the number is positive & the number is even"
isPositiveAndEven.IsSatisfiedBy(2).Assertions; // ["the number is positive", "the number is even"]

isPositiveAndEven.IsSatisfiedBy(3).Satisfied; // false
isPositiveAndEven.IsSatisfiedBy(3).Reason; // "the number is odd"
isPositiveAndEven.IsSatisfiedBy(3).Assertions; // ["the number is odd"]

isPositiveAndEven.IsSatisfiedBy(-2).Satisfied; // false
isPositiveAndEven.IsSatisfiedBy(-2).Reason; // "the number is negative"
isPositiveAndEven.IsSatisfiedBy(-2).Assertions; // ["the number is negative"]

isPositiveAndEven.IsSatisfiedBy(-5).Satisfied; // false
isPositiveAndEven.IsSatisfiedBy(-5.Reason; // "the number is negative & the number is odd"
isPositiveAndEven.IsSatisfiedBy(-5).Assertions; // ["the number is negative", "the number is odd"]
```


