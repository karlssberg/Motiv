# Supercharge your boolean logic

Motiv is a .NET library that helps you write expressive, maintainable, composable and testable boolean logic.
It provides a fluent API for building propositions, and it generates human-readable results that can be used in your 
application's user interface or to otherwise provide visibility into your application's decision-making process.

### Contents
1. [Spec](./Spec.md)
2. [Build()](./Build.md)
3. [WhenTrue()](./WhenTrue.md)
4. [WhenTrueYield()](./WhenTrueYield.md)
5. [WhenFalse()](./WhenFalse.md)
6. [WhenFalseYield()](./WhenFalseYield.md)
7. [Create()](./Create.md)
8. [As()](./As.md)
9. [Collections](./Collections.md)

## Installation
Motiv is available as a [Nuget Package](https://www.nuget.org/packages/Motiv/) that can be install it via NuGet Package 
Manager Console by running the following command:

```bash
Install-Package Motiv
```
or by using the .NET CLI:
```bash
dotnet add package Motiv
```

## Usage

### Simple 

Here's a simple example of how you can use Motiv to build a proposition that checks if a number is even.

```csharp
// define a proposition
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("is even")
        .WhenFalse("is odd")
        .Create();

// evaluate a model against the proposition
var result = isEven.IsSatisfiedBy(2);

result.Satisfied;  // true
result.Reason;     // "is even"
result.Assertions; // ["is even"]
```

### Advanced

However, Motiv's strengths really start to show as we scale up.

The following is an example of solving the famous [Fizz buzz](https://en.wikipedia.org/wiki/Fizz_buzz) task using 
Motiv.  If you are unfamiliar, numbers that are multiples of 3 are replaced with "fizz", numbers that are multiples of 5
are replaced with "buzz", and numbers that are multiples of both 3 and 5 are replaced with "fizzbuzz".
If none of these conditions are met, the number is returned as a string.

```csharp
var isFizz = 
    Spec.Build((int n) => n % 3 == 0)
        .Create("fizz");

var isBuzz =
    Spec.Build((int n) => n % 5 == 0)  
        .Create("buzz");

var isFizzAndBuzz =
    Spec.Build(isFizz & isBuzz)
        .Create("fizzbuzz");

var isSubstitution = 
    Spec.Build(isFizzAndBuzz.OrElse(isFizz | isBuzz))
        .WhenTrue((_, result) => result.Reason)
        .WhenFalse(n => n.ToString())
        .Create("is a fizzbuzz substitution");

isSubstitution.IsSatisfiedBy(2).Satisfied;   // false
isSubstitution.IsSatisfiedBy(2).Reason;      // "2"

isSubstitution.IsSatisfiedBy(3).Satisfied;   // true
isSubstitution.IsSatisfiedBy(3).Reason;      // "fizz"

isSubstitution.IsSatisfiedBy(4).Satisfied;   // false
isSubstitution.IsSatisfiedBy(4).Reason;      // "4"

isSubstitution.IsSatisfiedBy(5).Satisfied;   // true
isSubstitution.IsSatisfiedBy(5).Reason;      // "buzz"

isSubstitution.IsSatisfiedBy(15).Satisfied;  // true
isSubstitution.IsSatisfiedBy(15).Reason;     // "fizzbuzz"
```

Whilst more optimal solutions to Fizz Buzz exist, this example demonstrates the ease with which we can compose
complex propositions from simpler ones.

## Should I use Motiv?

Motiv is not meant to be a wholesale replacement of regular boolean logic.
If your logic is sufficiently simple or does not really require any feedback regarding decisions made, then there is 
probably no real tangible benefit of using Motiv.

Its utility, however, will likely become clear if two or more of the following are desired:

1. **Visibility**: You need to provide feedback in real-time regarding why a certain condition was met (or not).
2. **Decomposition**: Your logic is either too complex or deeply nested to understand at a glance, so it needs
   to be broken up in to meaningful parts.
3. **Reusability**: You wish to re-use your logic in multiple places without having to re-implement it.
4. **Modeling**: You need to explicitly model your domain logic.
5. **Testing**: You want to thoroughly test your logic without having to mock or stub out dependencies.

### Tradeoffs

1. **Performance**: Motiv is not designed for high-performance scenarios where nanoseconds matter.
   It is meant to be used in scenarios where maintainability and readability are paramount.
   That being said, for the majority of use-cases the performance overhead is truly negligible.
2. **Dependency**: This library is a dependency.
   Once embedded in your codebase it will be challenging to remove.
   However, this library does not itself depend on any third-party libraries, so it does not bring any unexpected
   baggage with it.
3. **Learning Curve**: For many, this library is a new approach and will nonetheless require a bit of familiarization.
   That being said, it has been deliberately designed to be as intuitive and easy to use as possible—there is
   relatively little to learn.