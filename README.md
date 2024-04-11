# Motiv
### Turn your if-statements into _why_ statements

Motiv is a .NET library that supercharges the developer experience when working with boolean logic.

At its core, it allows you to package your boolean expressions into strongly typed _propositions_.
By _propositions_, we mean a declarative statement that can be either true or false, such as _I think, therefore I am_,
or _email address is missing an @ symbol_.

A proposition can be combined with other propositions to form new ones, or it can be used to evaluate models to 
determine if they are satisfied by it.
```csharp
var isUsefulLibrary = new HasExplanations() & new HasCustomMetadata() & new IsReusable() & new IsComposable();

if (isUsefulLibrary.IsSatisfiedBy(new MyCriteria()))
{
    ...
```
When you evaluate a proposition, you get back a `BooleanResultBase` object that tells you whether the proposition was 
satisfied (or not) and also provides you with a `Reason` (and other useful information).
```csharp
var result = isUsefulLibrary.IsSatisfiedBy(new InferiorAlternative());

result.Satisfied; // false
result.Reason; // "no support for explanations & no support for custom metadata"
result.Assertions; // ["no support for explanations", "no support for custom metadata"]
```
Only those propositions that helped determine the outcome will be used to generate the `Reason`.

Propositions are simple to construct and are not limited to strings—you can optionally provide custom objects to 
propositions (referred to as `Metadata`). 

```csharp
var canCheckInSpec =
    Spec.Build<Passenger>((Passenger passenger) =>  
        passenger.HasValidTicket && 
        passenger.FlightTime.AddHours(-24) <=  DateTime.Now &&
        passenger.FlightTime.AddMinutes(-30) >= DateTime.Now
        passenger.OutstandingFees == 0)             // start with a predicate
    .WhenTrue("Passenger can check in")             // explain what it means to be true
    .WhenFalse("Passenger cannot check in")         // explain what it means to be false
    .Create("can check in");                        // describe the proposition

var result = canCheckInSpec.IsSatisfiedBy(passenger);
result.Metadata; // ["Passenger can check in"]
result.Reason; // "can check in"
result.Assertions; // ["Passenger can check in"]
```

Observe the `Spec` type.
This is a _specification_.
Specifications are the building blocks of propositions—they are the nodes of the logical expression tree.
It is common for the terms _specification_ and _proposition_ to be used interchangeably, but it is useful to understand 
how they differ.

The name comes from the _Specification Pattern_ in software design, which is a pattern that allows you to encapsulate
logic in a reusable and composable way.
However, the OO nature of the Specification Pattern can make the developer experience feel a bit verbose and cumbersome 
for what is typically a simple task.
Motiv aims to provide a more intuitive and productive way to work with specifications by making it a (mostly)
functional experience.

#### What can I use the metadata for?
* **User feedback** - You require an application to provide detailed and accurate feedback to the user about why a 
  certain decisions were made. 
* **Debugging** - Quickly understand why a certain condition was met, or not. This can be especially useful in complex 
  logical expressions where it might not be immediately clear which part of the expression was responsible for the final 
  result.
* **Multi-language Support** - The metadata doesn't have to be a string. It can be any type, which means you could use
  it to support multilingual explanations.
* **Rules-engine** - The metadata can be used to conditionally select stateful objects, which can be used to 
  implement a 
  rules-engine. This can be useful in scenarios where you need to apply different rules to different objects based on 
  their state.
* **Validation** - The metadata can be used to provide human-readable explanations of why a certain validation rule was 
  not met. This can be useful in scenarios where you need to provide feedback to the user about why a certain input 
  was not valid.
* **Parsing CLI arguments** - The command line arguments array can be interrogated and mapped to state 
  objects (metadata) to help conditionally drive behavior in the application.

## Usage

The following example is a basic demonstration of how to use Motiv.
It shows how to create a basic proposition and then use it to determine if a number is negative or not.

### Basic proposition
A basic proposition can be created using the `Spec` class. This class provides a fluent API for creating a 
logical proposition

```csharp
var isEligibleForLoanSpec =
    Spec.Build((Customer customer) => customer.CreditScore > 600 && customer.Balance > 5000)
        .Create("is eligible for loan");
```

This can then be evaluated by calling the `IsSatisfiedBy()` method and passing in a model to evaluate.
```csharp
var eligibleCustomer = new Customer 
{ 
    CreditScore = 700, 
    Balance = 6000
};

var isEligibleForLoan = isEligibleForLoanSpec.IsSatisfiedBy(eligibleCustomer); // evaluate

isEligibleForLoan.Satisfied; // true
isEligibleForLoan.Reason; // "is eligible for loan"
isEligibleForLoan.Assertions; // "is eligible for loan"
```

When negated, a basic proposition will return a reason prefixed with a `!` character.
This is useful for debugging purposes.

```csharp
var isEligibleForLoan = isEligibleForLoanSpec.IsSatisfiedBy(new Customer { Balance = 4000 });

isEligibleForLoan.Satisfied; // true
isEligibleForLoan.Reason; // "is eligible for loan"
isEligibleForLoan.Assertions; // "is eligible for loan"
```

You can also use the `WhenTrue()` and `WhenFalse()` methods to provide a more human-readable description for when the 
outcome is either `true` or `false`.
These values will be used in the `Reason` and `Assertions` properties of the result.

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create();

var isNegative = isNegativeSpec.IsSatisfiedBy(-3);

isNegative.Satisfied; // true
isNegative.Reason; // "the number is negative"
isNegative.Assertions; // ["the number is negative"]
```

If for whatever reason it is not convenient to use the strings supplied to the `WhenTrue()` and `WhenFalse()` as a 
`Reason`, you can instead provide a propositional statement to the `Create()` method.
This will then be used in the `Reason` property.  This can be useful if the text supplied to the `WhenTrue()` and 
`WhenFalse()` is particularly verbose, or doesn't make sense as a `Reason`.

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create("is negative");

var isNegative = isNegativeSpec.IsSatisfiedBy(-3);

isNegative.Satisfied; // true
isNegative.Reason; // "is negative"
isNegative.Assertions; // ["the number is negative"]
```

You are also not limited to strings.
You can equally supply any POCO object and it will be yielded when appropriate.

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .WhenTrue(new MyClass { Message = "the number is negative" })
        .WhenFalse(new MyClass { Message = "the number is not negative" })
        .Create("is negative")

var isNegative = isNegativeSpec.IsSatisfiedBy(-3);

isNegative.Satisfied; // true
isNegative.Reason; // "is negative"
isNegative.Assertions; // ["is negative"]
isNegative.Metadata; // [{ Message = "the number is negative" }]
````

### Combining propositions
The real power of Motiv comes from combining propositions to form new ones.
The library will take care of collating the underlying causes and filter out irrelevant and inconsequential 
assertions and metadata from the final result.
propositions can be combined using the `&`,`|` and `^` operators as well as the supplemental `.OrElse()` and
`.AndAlso()` methods.

```csharp
var hasValidTicketSpec =
    Spec.Build((Passenger passenger) => passenger.HasValidTicket)
        .WhenTrue("has a valid ticket")
        .WhenFalse("does not have a valid ticket")
        .Create();

var hasOutstandingFeesSpec =
    Spec.Build((Passenger passenger) => passenger.OutstandingFees > 0)
        .WhenTrue("has outstanding fees")
        .WhenFalse("does not have outstanding fees")
        .Create();

var isCheckInOpenSpec =
    Spec.Build((Passenger passenger) =>
        passenger.FlightTime - DateTime.Now <= TimeSpan.FromHours(4) &&
        passenger.FlightTime - DateTime.Now >= TimeSpan.FromMinutes(30))
            .WhenTrue("check-in is open")
            .WhenFalse("check-in is closed")
            .Create();

var canCheckInSpec = hasValidTicketSpec & !hasOutstandingFeesSpec & isCheckInOpenSpec;

var canCheckIn = canCheckInSpec.IsSatisfiedBy(validPassenger);

canCheckIn.Satisfied; // true
canCheckIn.Reason; // "has a valid ticket & does not have outstanding fees & check-in is open"
canCheckIn.Assertions; // ["has a valid ticket", "does not have outstanding fees", "check-in is open"]
```

When combining propositions to form new ones, only the propositions that helped determine the final result 
will be included in the `Assertions` property and `Reason` property.

```csharp
var isPositiveAndOdd = isPositiveAndOddSpec.IsSatisfiedBy(-3);

isPositiveAndOdd.Satisfied; // returns false
isPositiveAndOdd.Reason; // "is negative"
isPositiveAndOdd.Assertions; // ["the number is negative"]
```

### Encapsulation and Re-use

#### Redefining propositions
Sometimes an existing propositions do not produce the desired assertions or metadata.
In this case, you will need to wrap the existing proposition in a new one.

```csharp
var underlying = 
    Spec.Build((int n) => n < 0)
        .WhenTrue(new MyClass { Message = "the number is negative" })
        .WhenFalse(new MyClass { Message = "the number is not negative" })
        .Create("is negative (metadata)");

var isNegative =
    Spec.Build(underlying)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create("is negative (explanation)");
```

#### Strongly typed proposition
You will likely want to encapsulate propositions for reuse across an application.
For this you typically have two options, which is to either return `Spec` instances from members of POCO 
objects, or to derive from the `Spec<TModel>` or `Spec<TModel, TMetadata>` class (the former being merely syntactic 
sugar for `Spec<TModel, string>`).
Using these classes will help you to maintain a separation of concerns and also raise the conspicuity of important 
logic within an application. 

```csharp
public class IsNegativeSpec : Spec<int>(
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create());

public class IsNegativeMultiLingualSpec : Spec<int, MyClass>(
    Spec.Build((int n) => n < 0)
        .WhenTrue(new MyClass { Spanish = "el número es negativo" })
        .WhenFalse(new MyClass { Spanish = "el número no es negativo" })
        .Create("is negative"));
```

If you require (or prefer) your proposition to be expressed as multiple statements you can define them within a 
factory method.

```csharp
public class IsPositiveAndOddSpec : Spec<int>(() => 
    {
        var isNegativeSpec =
            Spec.Build((int n) => n < 0)
                .Create("is negative");+

        var isEvenSpec =
            Spec.Build((int n) => n % 2 == 0)
                .Create("is even"); 

        return !isNegativeSpec & !isEvenSpec;
    });
```

### Higher Order Logic
To perform logic over collections of models, higher order logical operations are required.
This library provides a `.As()` builder method that allows you to define your own higher order logical operations.
Some built-in higher order logical operations are provided for popular operations, but you can also add your own using
extension methods.

The current built-in higher order logical operations are:
- `AsAllSatisfied()`: Creates a proposition that yields a true boolean-result object if all the models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAnySatisfied()`: Creates a proposition that yields a true boolean-result object if any of the models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsNoneSatisfied()`: Creates a proposition that yields a true boolean-result object if none of the models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsNSatisfied()`: Creates a proposition that yields a true boolean-result object if exactly N models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAtLeastNSatisfied()`: Creates a proposition that yields a true boolean-result object if at least N models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAtMostfNSatisfied()`: Creates a proposition that yields a true boolean-result object if at most N models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.

```csharp
Spec.Build((int n) => n < 0)
    .AsAllSatisfied()
    .WhenTrue("all are negative")
    .WhenFalse("some are not negative")
    .Create();
```

You can also use an existing proposition instead of a predicate to create a higher order logical operation.
This will give you access to each result and model pair, which can be used to customize the output to a 
particular use-case.

```csharp
Spec.Build(new IsNegativeIntegerSpec())
    .AsAllSatisfied()
    .WhenTrue("all are negative")
    .WhenFalse(evaluation => evaluation.FalseModels.Select(n => $"{n} is not negative"))
    .Create();
```
When dynamically generating assertions/metadata, you are provided with an _evaluation_ object that contains 
pre-defined properties that can be used to customize the output (such as `TrueModels`, `FalseModels`, `TrueCount`, 
`FalseCount` etc.).
This is to facilitate pattern matching using switch expressions, which results in more readable inline conditional 
checks.

```csharp
var allAreNegative =
    Spec.Build(new IsNegativeIntegerSpec())
        .AsAllSatisfied()
        .WhenTrue(eval => eval switch
        {
            { Count: 0 } => "there is an absence of numbers",
            { Models: [< 0 and var n] } => $"{n} is negative and is the only number",
            _ => "all are negative numbers"
        })
        .WhenFalse(eval => eval switch
        {
            { Models: [0] } => ["the number is 0 and is the only number"],
            { Models: [> 0 and var n] } => [$"{n} is positive and is the only number"],
            { NoneSatisfied: true } when eval.Models.All(m => m is 0) => ["all are 0"],
            { NoneSatisfied: true } when eval.Models.All(m => m > 0) => ["all are positive numbers"],
            { NoneSatisfied: true } =>  ["none are negative numbers"],
            _ => eval.FalseResults.GetAssertions()
        })
        .Create("all are negative");

allAreNegative.IsSatisfiedBy([]).Assertions; // ["there is an absence of numbers"]
allAreNegative.IsSatisfiedBy([-10]).Assertions; // ["-10 is negative and is the only number"]
allAreNegative.IsSatisfiedBy([-2, -4, -6, -8]).Assertions; // ["all are negative numbers"]
allAreNegative.IsSatisfiedBy([0]).Assertions; // ["the number is 0 and is the only number"]
allAreNegative.IsSatisfiedBy([11]).Assertions; // ["11 is positive and is the only number"]
allAreNegative.IsSatisfiedBy([0, 0, 0, 0]).Assertions; // ["all are 0"]
allAreNegative.IsSatisfiedBy([2, 4, 6, 8]).Assertions; // ["all are positive numbers"]
allAreNegative.IsSatisfiedBy([0, 1, 2, 3]).Assertions; // ["none are negative numbers"]
allAreNegative.IsSatisfiedBy([-2, -4, 0, 9]).Assertions; // ["0 is not negative", "9 is not negative"]
```

## Tradeoffs
There are inevitably potential tradeoffs to consider when using this library.
1. **Performance**: This library is designed to be as performant as possible, but it is still a layer of abstraction
   over the top of your logic.
   This means that there is a measurable performance cost to using it.
   However, this cost is negligible in most cases and is generally eclipsed by the benefits it provides.
2. **Dependency**: This library is a dependency that you will have to manage.
   Once embedded in your codebase it will be challenging to remove. 
   However, this library does not itself depend on any third-party libraries, so it does not bring any unexpected 
   baggage with it. 
3. **Learning Curve**: For many, this library is a new approach and will nonetheless require a bit of familiarization.
   That being said, it has been deliberately designed to be as intuitive and easy to use as possible—there is 
   relatively little to learn.

## Getting Started with CLI

This section provides instructions on how to build and run the Motiv project using the .NET Core CLI, which is a
powerful and flexible way to work with .NET projects.

#### Prerequisites

- Ensure you have the [.NET SDK](https://dotnet.microsoft.com/download) installed on your machine.
- Clone the repository to your local machine.

#### Building the Project

1. **Open Terminal or Command Prompt**: Navigate to the directory where you cloned the Motiv repository.
2. **Navigate to the Project Directory**: If the solution file (`.sln`) is not in the root, navigate to the directory
   containing the solution file.
3. **Build the Solution**: Run the following command to build the solution:
   ```bash
   dotnet build
   ```

#### Running Tests

**Run Unit Tests** To execute tests within the solution run the following command:

```bash
dotnet test
```

## Contribution

Your contributions to Motiv are greatly appreciated:

### Branching Strategy

     main
        This is the primary branch of the repository. It should always be stable and deployable. 

     develop
        Merged into: main
        Purpose: This branch serves as an integration branch for features. Once a feature is complete, it is merged 
        into develop.  When develop is stable and ready for a release, its contents are merged into main.

     feature/
        Created from: develop
        Merged back into: develop
        Naming convention: feature/ followed by a descriptive name (e.g., feature/add-login)
        Purpose: Used for developing new features. Each feature should have its own branch.

Workflow Summary

    Start a new feature by creating a feature/ branch off develop.
    Once the feature is complete, create a pull request to merge it back into develop.
    Regularly merge develop into release branches for preparing releases.

Additional Notes

    Delete branches post-merge to keep the repository clean.
    Use pull requests for code review and ensure CI checks pass before merging.
    Regularly update branches with the latest changes from their parent branch to avoid large merge conflicts.

This strategy helps in maintaining a clean and manageable workflow, ensuring stability in the main branch, and
enabling continuous development and quick fixes as needed.

## License

MIT License

Copyright (c) 2023 karlssberg

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
