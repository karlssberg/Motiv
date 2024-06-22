# Motiv
![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)

## Transform Your If-Statements into Why-Statements

Motiv is a .NET library that enhances boolean logic by allowing you to model expressions as strongly typed
[propositions](https://en.wikipedia.org/wiki/Proposition).
A proposition is a declarative statement that can be evaluated as either true or false.

Examples include:
* _The sun is shining_
* _Email address is missing an @ symbol_
* _Subscription is within grace period_

Here's a simple example of a proposition:

```csharp
Spec.Build((int n) => n == 0).Create("empty");
```

With Motiv, you can compose propositions using boolean operators (`&`, `|`, `^`)
and receive concise explanations about the evaluation results.

```csharp
// Define propositions
var isValid = Spec.Build((int n) => n is >= 0 and <= 11).Create("valid");
var isEmpty = Spec.Build((int n) => n == 0).Create("empty");
var isFull  = Spec.Build((int n) => n == 11).Create("full");

// Compose a new proposition
var isPartiallyFull = isValid & !(isEmpty | isFull);

// Evaluate the proposition
var result = isPartiallyFull.IsSatisfiedBy(5);

result.Satisfied;   // true
result.Assertions;  // ["valid", "!empty", "!full"]
```

### Useful Links
- [Documentation](https://karlssberg.github.io/Motiv/)
- [NuGet Package](https://www.nuget.org/packages/Motiv/)
- [Specification Pattern](https://en.wikipedia.org/wiki/Specification_pattern)
- [Propositions](https://en.wikipedia.org/wiki/Proposition)
- [Official GitHub Repository](https://github.com/karlssberg/Motiv)

---

## Why Motiv?

Motiv primarily aims to provide visibility into your application's decision-making process.
By decomposing expressions into propositions,
it addresses important architectural concerns and enables more advanced use cases, such as implementing rules engines.

Consider using Motiv if your project requires two or more of the following:

1. **Visibility**: Provide detailed, real-time feedback about decisions made.
2. **Decomposition**: Break down complex or nested logic into meaningful subclauses for improved readability.
3. **Reusability**: Reuse logic across multiple locations to reduce duplication.
4. **Modeling**: Explicitly model your domain logic (e.g.,
   [Domain Driven Design](https://en.wikipedia.org/wiki/Domain-driven_design)).
5. **Testing**: Easily and thoroughly test your logic without the need for mocking or stubbing dependencies.

### The Limitation of Standard Booleans

Regular boolean expressions don't explain _why_ they evaluate to `true` or `false`.
For simple expressions, this isn't an issue.
However, with complex, multi-clause expressions,
determining the underlying cause at runtime can be challenging or even impossible.

### Beyond Design-Time

While boolean expressions are highly visible at design-time, they lack runtime visibility.
Motiv bridges this gap by providing insights into your logical expressions during execution.

### Function Decomposition vs. Motiv

Functions are excellent for encapsulating logic, but they don't inherently support logical composition.
Creating utility functions to address this essentially implements a functional version of the
[Specification pattern](https://en.wikipedia.org/wiki/Specification_pattern) –
similar to what Motiv does.

## What is Motiv?

Motiv is a functional-style implementation of the 
[Specification pattern](https://en.wikipedia.org/wiki/Specification_pattern).
It allows you to model your logic declaratively and compose it into new,
reusable expressions that provide detailed explanations about their evaluation results.

## Use Cases for Motiv

Motiv can be applied in various scenarios, including:

* **User Feedback**: Provide detailed and accurate explanations to users about decision outcomes.
* **Debugging**: Quickly understand why certain conditions were met (or not) in complex logic scenarios.
* **Multilingual Support**: Offer explanations in different languages.
* **Validation**: Ensure user input meets specific criteria and provide detailed feedback for failures.
* **Auditing**: Log _why_ something happened, not just _what_ happened.

Feel free to explore the library and discover innovative ways to use it in your projects.

## Installation

Install Motiv via NuGet Package Manager Console:
```bash
Install-Package Motiv
```
Or using the .NET CLI:
```bash
dotnet add package Motiv
```

## Usage

All propositions follow a similar pattern, starting with `Spec.Build()`.
While there are various overloads for different use-cases,
they all ultimately rely on a boolean predicate function: `Spec.Build(Func<TModel, bool> predicate)`.

### Basic Proposition

Create a basic proposition using `Spec.Build()` followed immediately by `Create()`:

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) => 
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .Create("eligible for loan");
```

Evaluate the proposition using `IsSatisfiedBy()`:

```csharp
var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);   

result.Satisfied;  // true
result.Reason;     // "eligible for loan"
result.Assertions; // ["eligible for loan"]
```

Negated propositions return reasons prefixed with `!`:

```csharp
var result = isEligibleForLoan.IsSatisfiedBy(uneligibleCustomer); 

result.Satisfied;  // false
result.Reason;     // "!eligible for loan"
result.Assertions; // ["!eligible for loan"]
```

### Propositions with Custom Assertions

Use `WhenTrue()` and `WhenFalse()` to provide user-friendly explanations:

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) => 
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue("customer is eligible for a loan")
        .WhenFalse("customer is not eligible for a loan")
        .Create();

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "customer is eligible for a loan"
result.Assertions; // ["customer is eligible for a loan"]
```

### Propositions with Custom Metadata

You can use any POCO object as metadata:

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) =>
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue(new MyMetadata("customer is eligible for a loan"))
        .WhenFalse(new MyMetadata("customer is not eligible for a loan"))
        .Create("eligible for loan");

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "eligible for loan"
result.Assertions; // ["eligible for loan"]
result.Metadata;   // [{ Message = "customer is eligible for a loan" }]
```

### Dynamic Explanations and Metadata

Use function overloads of `WhenTrue()` and `WhenFalse()` for dynamic explanations:

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) =>
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue(customer => $"customer {customer.Name} is eligible for a loan")
        .WhenFalse(customer => $"customer {customer.Name} is not eligible for a loan")
        .Create("eligible for loan");
```

### Composing Propositions

Combine propositions using `&`, `|`, `^`, `.OrElse()`, and `.AndAlso()`:

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
    
var isEligibleForLoan = hasGoodCreditScore & hasSufficientIncome; 

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "customer has a good credit score & customer has sufficient income"
result.Assertions; // ["customer has a good credit score", "customer has sufficient income"]
```

### Encapsulation and Reuse

#### Redefining Propositions

Wrap existing propositions to redefine them:

```csharp
var isEligibleForLoan =
    Spec.Build(hasGoodCreditScore & hasSufficientIncome)
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

Reuse assertions from original propositions:

```csharp
var isEligibleForLoan =
    Spec.Build(hasGoodCreditScore & hasSufficientIncome) 
        .WhenTrue("customer is eligible for a loan")
        .WhenFalseYield((_, result) => result.Assertions)
        .Create();

var ineligibleResult = isEligibleForLoan.IsSatisfiedBy(ineligibleCustomer);

ineligibleResult.Reason;        // "customer is eligible for a loan"
ineligibleResult.Assertions;    // ["customer has an inadequate credit score", "customer has insufficient income"]
ineligibleResult.Justification; // !customer is eligible for a loan
                                //     AND
                                //         customer has an inadequate credit score
                                //         customer has insufficient income
```

#### Strongly Typed Propositions

Create reusable, strongly-typed propositions by deriving from `Spec<TModel>` or `Spec<TModel, TMetadata>`:

```csharp
public class HasGoodCreditScoreProposition : Spec<Customer>
{
    public HasGoodCreditScoreProposition(int threshold) : base(
        Spec.Build((Customer customer) => customer.CreditScore > threshold)
            .WhenTrue("customer has a good credit score")
            .WhenFalse("customer has an inadequate credit score")
            .Create())
    {
    }
}

// Usage
var hasGoodScore = new HasGoodCreditScoreProposition(600);
var result = hasGoodScore.IsSatisfiedBy(customer);
```

### Higher Order Logic

Use higher order logic to reason about collections of models:

```csharp
var allNegative = Spec.Build((int n) => n < 0)
    .As(results => results.All(r => r.Satisfied))
    .WhenTrue("all are negative")
    .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is not negative"))
    .Create();

var result = allNegative.IsSatisfiedBy(new[] { -1, -2, -3 });
result.Satisfied;  // true
result.Assertions; // ["all are negative"]

var result2 = allNegative.IsSatisfiedBy(new[] { -1, 0, 1 });
result2.Satisfied;  // false
result2.Assertions; // ["0 is not negative", "1 is not negative"]
```

#### Built-in Higher Order Operations

Motiv provides several built-in higher order operations:

- `AsAllSatisfied()`
- `AsAnySatisfied()`
- `AsNoneSatisfied()`
- `AsNSatisfied()`
- `AsAtLeastNSatisfied()`
- `AsAtMostNSatisfied()`

Example:

```csharp
var allNegative = Spec.Build((int n) => n < 0)
    .AsAllSatisfied()
    .WhenTrue("all are negative")
    .WhenFalse("some are not negative")
    .Create();

var result = allNegative.IsSatisfiedBy(new[] { -1, -2, -3 });
result.Satisfied;  // true
result.Assertions; // ["all are negative"]
```

## Tradeoffs

Consider these potential tradeoffs when using Motiv:

1. **Performance**: Motiv isn't optimized for high-performance scenarios where nanoseconds matter.
   It's designed for maintainability and readability, with negligible overhead for most use-cases.
2. **Dependency**: Once integrated, Motiv becomes a core dependency in your codebase. However,
   it doesn't rely on third-party libraries, minimizing potential conflicts.
3. **Learning Curve**: While Motiv introduces a new approach,
   it's designed to be intuitive and easy to use, with a relatively shallow learning curve.


## License

Motiv is released under the MIT License. See the [LICENSE](LICENSE) file for details.
