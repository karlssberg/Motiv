# Motiv

![Build Status](https://github.com/karlssberg/Motiv/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/Motiv.svg)](https://www.nuget.org/packages/Motiv/) [![codecov](https://codecov.io/gh/karlssberg/Motiv/graph/badge.svg?token=XNN34D2JIP)](https://codecov.io/gh/karlssberg/Motiv)
### Quick Links

- [Documentation](https://karlssberg.github.io/Motiv/)
- [Try Motiv Online](https://dotnetfiddle.net/knykpD)
- [NuGet Package](https://www.nuget.org/packages/Motiv/)
- [Official GitHub Repository](https://github.com/karlssberg/Motiv)

## Decisions Made Clear

Motiv is a solution to the _[Boolean Blindness](https://existentialtype.wordpress.com/2011/03/15/boolean-blindness/)_
problem (which is the loss of information resulting from the reduction of logic to a single true or false value).
It achieves this by decomposing logical expressions into a syntax tree of atomic
[propositions](https://en.wikipedia.org/wiki/Proposition), so that during evaluation,
the causes of a decision can be preserved, and put to use.
In most cases this will be a human-readable explanation of the decision, but it could equally be used to surface state.

```csharp
// Define the proposition
var isInRange = Spec.From((int n) => n >= 1 & n <= 10)
                    .Create("in range");

// Evaluate proposition (elsewhere in your code)
var result = isInRange.IsSatisfiedBy(11);

result.Satisfied;  // false
result.Assertions; // ["n > 10"]
result.Reason;     // "¬in range"
```

## Why Use Motiv?

Motiv primarily gives you visibility into your application's decision-making process by replacing opaque boolean
expressions with semantically rich propositions.

Consider using Motiv if your project requires two or more of the following:

1. **Visibility**: Provide detailed, real-time feedback about decisions made.
2. **Decomposition**: Break down complex logic into meaningful subclauses for improved readability.
3. **Reusability**: Reuse logic across multiple locations to reduce duplication.
4. **Modeling**: Explicitly model the logic in your domain as propositions.
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

### From Lambda Expression Tree to Propositions

When given an `Expression<Func<T, bool>>`, Motiv will transform it into a syntax tree of propositions, with each
underlying sub-expression describing the outcome of the logic that it performed.

```csharp
```csharp
Expression<Func<Customer, bool>> expression = customer =>
    customer.CreditScore > 600 & customer.Income > 100000;

var isEligibleForLoan =
    Spec.From(expression)
        .Create("eligible for loan");

var result = isEligibleForLoan.IsSatisfiedBy(eligibleCustomer);

result.Satisfied;  // true
result.Reason;     // "eligible for loan"
result.Assertions; // ["customer.CreditScore > 600", "customer.Income > 100000"]
```

### From Lambda Expression to Propositions

Create and evaluate a `Func<T, bool>` proposition:

```csharp
Func<Customer, bool> expression = customer =>
    customer.CreditScore > 600 & customer.Income > 100000;

var isEligibleForLoan =
    Spec.Build(expression)
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
2. **Dependency**: Once integrated, Motiv becomes a dependency in your codebase.
3. **Learning Curve**: While Motiv introduces a new approach, it's designed to be intuitive and straightforward to use.

## License

Motiv is released under the MIT License. See the [LICENSE](./LICENSE) file for details.
