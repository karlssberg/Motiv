# Motiv
![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)

## Turn your if-statements into _why_ statements

Motiv is a .NET library that supercharges the experience of working with boolean logic.

It allows you to package your boolean expressions into strongly typed _propositions_.
By propositions, we mean a declarative statement that can be evaluated to either true or false.

Examples of propositions include:
* _the sun is shining_
* _email address is missing an @ symbol_
* _subscription is within grace period_

In Motiv, they look like this:
```csharp
Spec.Build((Subscription subscription) => 
        DateTime.Now is var now 
        && subscription.ExpiryDate < now
        && now < subscription.ExpiryDate.AddDays(7))      // predicate function
    .WhenTrue("subscription is within grace period")      // true assertion (and proposition statement)
    .WhenFalse("subscription is not within grace period") // false assertion
    .Create();
```

Propositions can be composed together using boolean operators, such as `&`, `|`, and `^`, and when evaluated will only
surface the reasons (or custom metadata) from propositions that determined the final result.

```csharp
// compose propositions
var expression = ((!hasSubscriptionExpired | isInGracePeriod) & !isSubscriptionCancelled) | isStaff;

// define a new proposition
var canViewContent = 
    Spec.Build(expression)
        .WhenTrue("can view content")
        .WhenFalse("cannot view content")
        .Create();

// evaluate proposition
var result = canViewContent.IsSatisfiedBy(subscription); 

result.Satisfied;     // true
result.Reason;        // "can view content"
result.Justification; // can view content
                      //    OR
                      //        AND
                      //            OR
                      //                subscription is within grace period
                      //            subscription is not cancelled
```

### What is wrong with regular booleans?

Primitive booleans will not explain why they are `true` or `false`. 
If your expression only has one clause, then you can easily figure it out, but when you have multiple clauses, you 
would have to forensically analyze the expression to determine the underlying cause(s), which is not always possible.

### How is this different from an if-statement?

When you have a logical expression with multiple clauses, to get a detailed explanation, you would 
need to split the expression up in to multiple sub-expressions (one for each clause) and evaluate them separately.
You then need to combine the sub-expressions by applying the outstanding boolean operators in the right order to 
determine if the expression as a whole is satisfied.
Secondly, to surface an explanation (or metadata), you will need to collate the explanations from the sub-expressions 
and filter out those that didn't materially affect the result.
It is safe to say that an if-statement does not do all this for you.

### How is this different from wrapping my logic in a function?

Wrapping your logic in a function is a good start when modeling propositions.
A function-per-clause approach is sensible, as it allows you to encapsulate related logic whilst separating your 
concerns.
However, as you start doing this, your code will start to look like an inferior version of the Specification pattern 
(which is the pattern that Motiv is based upon).
It only takes a few more modifications, like wrapping the function in a class that simplifies composition, before you 
finally have an implementation of the Specification pattern.

### What is Motiv exactly?

Motiv is a functional-ish/fluent version of the
[Specification pattern](https://en.wikipedia.org/wiki/Specification_pattern), that allows you to very easily model your 
logic in a declarative way and then re-compose it to form new expressions.
When it is time to evaluate whether a model is satisfied by the expression, the underlying causes are figured out 
on your behalf, and any data associated with them is subsequently surfaced (which is typically a textual explanation).

### What can I use the Motiv for?

Motiv can be used in a variety of scenarios, including:

* **User feedback** - You require an application to provide detailed and accurate feedback to the user about why 
  certain decisions were made. 
* **Debugging** - Quickly understand why a certain condition was met (or not). When faced with deeply nested 
  if-else statements it can be challenging to comprehend the bigger picture. Motiv gives you the wherewithal to 
  separate the implementation details from the big-picture logical expression.
* **Multilingual support** - Use custom POCO objects instead of strings to provide multi-language support.
* **Decomposing complex logic or domain rules** - Whether you are faithfully modelling domain logic, or just trying to 
  decompose an unwieldy logical expression, Motiv can help you to break it down into more manageable and 
  understandable parts.
* **Validation** - Ensure user input meets certain criteria and provide detailed feedback when it does not.  Because 
  of the approach Motiv takes, it makes it relatively straightforward to convert existing logic into validation logic.
* **Parsing CLI arguments** - The command line arguments array can be interrogated and mapped to state 
  objects (metadata) to help conditionally drive behavior in the application.

You are encouraged to explore the library and find new and innovative ways to use it.

## Installation

You can install Motiv via NuGet Package Manager Console by running the following command:
```bash
Install-Package Motiv
```
or by using the .NET CLI:
```bash
dotnet add package Motiv
```

## Usage

All propositions follow the same basic usage pattern that starts with a call `Spec.Build()`.
It has many overloads for different use-cases, but they all trace back eventually to a boolean predicate function—in 
other words a `Spec.Build(Func<TModel, bool> predicate)`.

### Basic proposition

A basic proposition can be created calling the `Spec.Build()` method and then calling the `Create()` method without 
calling any other builder methods in between.

For example:
```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) => 
            customer is               // predicate
            {
                CreditScore: > 600,
                Income: > 100000
            })                         
        .Create("eligible for loan"); // propositional statement
```

This can then be evaluated by calling the `IsSatisfiedBy()` method and passing in a model to evaluate.
```csharp
// evaluate proposition as satisfied
var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);   

result.Satisfied;  // true
result.Reason;     // "eligible for loan"
result.Assertions; // ["eligible for loan"]
```

When negated, a basic proposition will return a reason prefixed with a `!` character.
This is useful for debugging purposes.

```csharp
// evaluate proposition as unsatisifed
var result = isEligibleForLoan.IsSatisfiedBy(uneligibleCustomer); 

result.Satisfied;  // false
result.Reason;     // "!eligible for loan"
result.Assertions; // ["!eligible for loan"]
```

Basic propositions are useful for encapsulation and debugging purposes, but their explanations are not particularly
user-friendly.

### Propositions with assertions

You can use the `WhenTrue()` and `WhenFalse()` methods to provide user-friendly explanations about the result.
These values will be used in the `Reason` and `Assertions` properties of the result.

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) => 
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue("customer is eligible for a loan")      // yield assertion when true
        .WhenFalse("customer is not eligible for a loan") // yield assertion when false
        .Create();

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "customer is eligible for a loan"
result.Assertions; // ["customer is eligible for a loan"]
```

### Propositions with custom metadata

You are also not limited to strings.
You can equally supply any POCO object, and it will be yielded when appropriate.

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) =>
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue(new MyMetadata("customer is eligible for a loan"))      // yield POCO object
        .WhenFalse(new MyMetadata("customer is not eligible for a loan")) // yield POCO object
        .Create("eligible for loan");

var result = isNegative.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "eligible for loan"
result.Assertions; // ["eligible for loan"]
result.Metadata;   // [{ Message = "customer is eligible for a loan" }]
````

### Dynamic explanations (and metadata)

There will be times when you need to provide a more dynamic explanation (or metadata object).
There are overloads to the `WhenTrue()` and `WhenFalse()` methods that allow you to provide a function that will be
evaluated when the proposition is satisfied.
These functions can be used to dynamically generate explanations or metadata based on the model being evaluated.
```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) =>
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue(customer => $"customer {customer.Name} is eligible for a loan")      // dynamic
        .WhenFalse(customer => $"customer {customer.Name} is not eligible for a loan") // dynamic
        .Create("eligible for loan");
```

### Composing propositions

The real power of Motiv comes from composing propositions to form new ones.
The library will take care of collating the underlying causes and filter out irrelevant and inconsequential 
assertions and metadata from the final result. 
Propositions can be composed using the `&`,`|` and `^` operators as well as the supplemental `.OrElse()` and
`.AndAlso()` methods.
This allows you to create explanations at various levels of granularity.

In our example, we can break down into its constituent parts, each with their own explanation.
```csharp
var hasGoodCreditScore =
    Spec.Build((Customer customer) => customer.CreditScore > 600)
        .WhenTrue("customer has a good credit score")
        .WhenFalse("customer has an inadequate credit score")
        .Create();

var hasSufficientIncome =
    Spec.Build((Customer customer) => customer.Income > 100000)
        .WhenTrue("customer has sufficient income")
        .WhenFalse("customer has insufficient income")
        .Create();
    
// compose propositions
var isEligibleForLoan = hasGoodCreditScore & hasSufficientIncome; 

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "customer has a good credit score & customer has sufficient income"
result.Assertions; // ["customer has a good credit score", "customer has sufficient income"]
```

When composing propositions to form new ones, only the propositions that helped determine the final result 
will be included in the `Assertions` property and `Reason` property.

```csharp
var result = isPositiveAndOddProposition.IsSatisfiedBy(ineligibleCustomer);

result.Satisfied;  // false
result.Reason;     // "customer has an inadequate credit score"
result.Assertions; // ["customer has an inadequate credit score"]
```

### Encapsulation and Re-use

#### Redefining propositions

Sometimes existing propositions do not produce the desired assertions or metadata.
In this case, you will need to wrap the existing proposition in a new one.

```csharp
var isEligibleForLoan =
    Spec.Build(hasGoodCreditScore & hasSufficientIncome) // reusing existing propositions
        .WhenTrue("customer is eligible for a loan")
        .WhenFalse("customer is not eligible for a loan")
        .Create();

var eligibleResult = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

eligibleResult.Reason;        // "customer is eligible for a loan"
eligibleResult.Assertions;    // ["customer is eligible for a loan"]
eligibleResult.Justification; // customer is eligible for a loan
                              //     AND
                              //         customer has a good credit score
                              //         customer has sufficient income
```

When deriving new propositions, you may still require assertions or metadata from the original.
```csharp
var isEligibleForLoan =
    Spec.Build(hasGoodCreditScore & hasSufficientIncome) 
        .WhenTrue("customer is eligible for a loan")
        .WhenFalseYield((_, result) => result.Assertions)     // reusing assertions
        .Create();

var ineligibleResult = isEligibleForLoan.IsSatisfiedBy(ineligibleCustomer);

ineligibleResult.Reason;        // "customer is eligible for a loan"
ineligibleResult.Assertions;    // ["customer has an inadequate credit score", "customer has insufficient income"]
ineligibleResult.Justification; // !customer is eligible for a loan
                                //     AND
                                //         customer has an inadequate credit score
                                //         customer has insufficient income
```

#### Strongly typed proposition

You will likely want to encapsulate propositions for reuse across your application.
For this you typically have two options, which is to either return `Spec` instances from members of POCO 
objects, or to derive from the `Spec<TModel>` or `Spec<TModel, TMetadata>` class (the former being merely syntactic 
sugar for `Spec<TModel, string>`).
By creating a new class that derives from `Spec<TModel>` or `Spec<TModel, TMetadata>`, the proposition becomes a
unique type within the type-system, which is necessary in some situation, such as with dependency injection frameworks.
Using the above classes will help you to maintain a separation of concerns and also raise the conspicuity of important 
logic within your application. 

```csharp
public class HasGoodCreditScoreProposition(int threshold) : Spec<int>(  // declare as a unique type
    Spec.Build((Customer customer) => customer.CreditScore > threshold)
        .WhenTrue("customer has a good credit score")
        .WhenFalse("customer has an inadequate credit score")
        .Create());
```
or if a custom object is required for metadata, then you can use the `Spec<TModel, TMetadata>` class instead:
```csharp
public class HasSufficientIncomeProposition(decimal threshold) : Spec<int, MyMetadata>( // use MyMetadata type
    Spec.Build((Customer customer) => customer.Income > threshold)
        .WhenTrue(new MyMetadata("customer has sufficient income"))    // custom metadata
        .WhenFalse(new MyMetadata("customer has insufficient income")) // custom metadata
        .Create("has sufficient income"));
```

### Higher Order Logic

To perform logic over collections of models, higher order logical operations are required.
Whilst you can create a first-order proposition that operates on collections of models, yielding explanations (or 
metadata) would be challenging.
This library provides a `.As()` builder method to address this.

#### Built-in higher order logic

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
    .AsAllSatisfied()               // higher order logic
    .WhenTrue("all are negative")
    .WhenFalse("some are not negative")
    .Create();
```

You can also use an existing proposition instead of a predicate to create a higher order logical operation.
This will give you access to each result and model pair, which can be used to customize the output to a 
particular use-case.

```csharp
Spec.Build(new IsNegativeIntegerProposition()) // existing proposition
    .AsAllSatisfied()
    .WhenTrue("all are negative")
    .WhenFalseYield(evaluation => evaluation.FalseModels.Select(n => $"{n} is not negative"))
    .Create();
```

#### Dynamic

When dynamically generating assertions/metadata, you are provided with an _evaluation_ object that contains 
pre-defined properties that can be used to customize the output (such as `TrueModels`, `FalseModels`, `TrueCount`, 
`FalseCount` etc.).
This is to facilitate pattern matching using switch expressions, which results in more readable inline conditional 
checks.

```csharp
var allNegative =
    Spec.Build(new IsNegativeIntegerProposition())
        .AsAllSatisfied()
        .WhenTrue(eval => 
            eval switch
            {
                { Count: 0 } => "there is an absence of numbers",
                { Models: [< 0 and var n] } => $"{n} is negative and is the only number",
                _ => "all are negative numbers"
            })
        .WhenFalseYield(eval =>
            eval switch
            {
                { Models: [0] } => ["the number is 0 and is the only number"],
                { Models: [> 0 and var n] } => [$"{n} is positive and is the only number"],
                { NoneSatisfied: true } when eval.Models.All(m => m is 0) => ["all are 0"],
                { NoneSatisfied: true } when eval.Models.All(m => m > 0) => ["all are positive numbers"],
                { NoneSatisfied: true } =>  ["none are negative numbers"],
                _ => eval.FalseResults.GetAssertions()
            })
        .Create("all are negative");

allNegative.IsSatisfiedBy([]).Assertions;               // ["there is an absence of numbers"]
allNegative.IsSatisfiedBy([-10]).Assertions;            // ["-10 is negative and is the only number"]
allNegative.IsSatisfiedBy([-2, -4, -6, -8]).Assertions; // ["all are negative numbers"]
allNegative.IsSatisfiedBy([0]).Assertions;              // ["the number is 0 and is the only number"]
allNegative.IsSatisfiedBy([11]).Assertions;             // ["11 is positive and is the only number"]
allNegative.IsSatisfiedBy([0, 0, 0, 0]).Assertions;     // ["all are 0"]
allNegative.IsSatisfiedBy([2, 4, 6, 8]).Assertions;     // ["all are positive numbers"]
allNegative.IsSatisfiedBy([0, 1, 2, 3]).Assertions;     // ["none are negative numbers"]
allNegative.IsSatisfiedBy([-2, -4, 0, 9]).Assertions;   // ["0 is not negative", "9 is not negative"]
```

## Tradeoffs

There are inevitably potential tradeoffs to consider when using this library.
1. **Performance**: This library is designed to be as performant as possible, but it is still a layer of abstraction
   over the top of native logic.
   This means that there is nevertheless a measurable performance cost compared to computing boolean values natively.
   This cost is negligible in most cases and is generally eclipsed by the benefits provided.
2. **Dependency**: This library is a dependency.
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

Copyright (c) 2024 karlssberg

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
