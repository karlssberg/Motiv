# Motiv

![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)

## Know _Why_, not just _What_

Motiv is a developer-first .NET library that transforms the way you work with boolean logic.
It lets you form expressions from discrete [propositions](https://en.wikipedia.org/wiki/Proposition) so that you
can explain _why_ decisions were made.

First create [atomic propositions](https://en.wikipedia.org/wiki/Atomic_sentence):

```csharp
// Define atomic propositions
var isValid = Spec.Build((int n) => n is >= 0 and <= 11).Create("valid");
var isEmpty = Spec.Build((int n) => n == 0).Create("empty");
var isFull  = Spec.Build((int n) => n == 11).Create("full");
```

Then compose using operators (e.g., `!`,  `&`, `|`, `^`):

```csharp
// Compose a new ad-hoc proposition
var composed = isValid & !(isEmpty | isFull);

// Give it a new name
var isPartiallyFull = Spec.Build(composed).Create("partial");
```

To get detailed feedback:

```csharp
// Evaluate the proposition against a model/value
var result = isPartiallyFull.IsSatisfiedBy(5);

result.Satisfied;         // true
result.Reason;            // "partial"
result.UnderlyingReasons; // ["valid & !(¬empty | ¬full)"]
result.SubAssertions;     // ["valid", "¬empty", "¬full"]
result.Justifications     // partial
                          //     AND
                          //         valid
                          //         NOR
                          //             ¬empty
                          //             ¬full
```

## Why Use Motiv?

Motiv primarily gives you visibility into your application's decision-making process.
By decomposing expressions into propositions,
it addresses important architectural concerns and enables more advanced use cases,
such as implementing dynamic logic or determining state.

Consider using Motiv if your project requires two or more of the following:

1. **Visibility**: Provide detailed, real-time feedback about decisions made.
2. **Decomposition**: Break down complex logic into meaningful subclauses for improved readability.
3. **Reusability**: Reuse logic across multiple locations to reduce duplication.
4. **Modeling**: Explicitly model your domain logic.
5. **Testing**: Simplify the testing your logic without mocking or stubbing dependencies.

## Use Cases

Motiv can be applied in various scenarios, including:

* **User Feedback**: Provide detailed explanations about decision outcomes.
* **Debugging**: Quickly find out the causes from complex logic.
* **Multilingual Support**: Offer explanations in different languages.
* **Validation**: Ensure user input meets specific criteria and provide detailed feedback.
* **Dynamic Logic**: Compose logic at runtime based on user input.
* **Rules Processing**: Declaratively define and compose complex _if-then_ rules.
* **Conditional State**: Yield different states based on complex criteria.
* **Auditing**: Log _why_ something happened, instead of _what_ happened.

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

### Basic Proposition

Create and evaluate a basic proposition:

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) =>
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .Create("eligible for loan");

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "eligible for loan"
result.Assertions; // ["eligible for loan"]
```

### Propositions with Custom Assertions

Use `WhenTrue()` and `WhenFalse()` for user-friendly explanations:

```csharp
var isEligibleForLoan =
    Spec.Build((Customer customer) =>
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue("eligible for a loan")
        .WhenFalse("not eligible for a loan")
        .Create();

var result = isEligibleForLoanPolicy.IsSatisfiedBy(ineligibleCustomer);

result.Satisfied;  // false
result.Reason;     // "not eligible for a loan"
```

### Propositions with Custom Metadata

Use `WhenTrue()` and `WhenFalse()` with types other than `string`:

```csharp
var isEligibleForLoanPolicy =
    Spec.Build((Customer customer) =>
            customer is
            {
                CreditScore: > 600,
                Income: > 100000
            })
        .WhenTrue(MyEnum.EligibleForLoan)
        .WhenFalse(MyEnum.NotEligibleForLoan)
        .Create("eligible for a loan");

var result = isEligibleForLoanPolicy.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Value;      // MyEnum.EligibleForLoan
result.Reason;     // "eligible for a loan"
```

### Composing Propositions

Combine propositions using boolean operators:

```csharp
var hasGoodCreditScore =
    Spec.Build((Customer customer) => customer.CreditScore > 600)
        .WhenTrue("good credit score")
        .WhenFalse("inadequate credit score")
        .Create();

var hasSufficientIncome =
    Spec.Build((Customer customer) => customer.Income > 100000)
        .WhenTrue("sufficient income")
        .WhenFalse("insufficient income")
        .Create();

var isEligibleForLoan = hasGoodCreditScore & hasSufficientIncome;

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "good credit score & sufficient income"
result.Assertions; // ["good credit score", "sufficient income"]
```

### Higher Order Logic

Provide facts about collections:

```csharp
var allNegative =
    Spec.Build((int n) => n < 0)
        .AsAllSatisfied()
        .WhenTrue("all are negative")
        .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is not negative"))
        .Create();

var result = allNegative.IsSatisfiedBy([-1, 2, 3]);

result.Satisfied;  // false
result.Reason;     // "¬all are negative"
result.Assertions; // ["2 is not negative", "3 is not negative"]
```

## Tradeoffs

Consider these potential tradeoffs when using Motiv:

1. **Performance**: Motiv isn't optimized for high-performance scenarios where nanoseconds matter.
2. **Dependency**: Once integrated, Motiv becomes a core dependency in your codebase.
3. **Learning Curve**: While Motiv introduces a new approach, it's designed to be intuitive and easy to use.

## License

Motiv is released under the MIT License. See the [LICENSE](./LICENSE) file for details.

## Resources

- [Documentation](https://karlssberg.github.io/Motiv/)
- [NuGet Package](https://www.nuget.org/packages/Motiv/)
- [Official GitHub Repository](https://github.com/karlssberg/Motiv)
