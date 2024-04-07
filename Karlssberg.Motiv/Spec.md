# Specifications (aka `Spec`)
Specifications combine to form logical syntax trees, which we call _propositions_.
There are three main ways to create a specification.
One is by using the logical operators (such as `&`, `|`,  `^`etc.).
The Second is by using the `Spec.Build()` method.
The third way, deriving from the `Spec<TModel>` or `Spec<TModel, TMetadata>` types
(where the `Spec<TModel>` type is a syntactic sugar for the `Spec<TModel, string>` type).

### The `Spec` type
This type is only used for building propositions using the logical operators.
As it is `static` it cannot be used to derive new types, but it can be used to compose new specifications.
```csharp
var isEvenSpec = 
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("even")
        .WhenFalse("odd")
        .Create();

var isPositiveSpec =
    Spec.Build((int n) => n > 0)
        .WhenTrue("positive")
        .WhenFalse("not positive")
        .Create();

var isEvenAndPositiveSpec = 
    Spec.Build(isEvenSpec & isPositiveSpec)
        .WhenTrue("the number is even and positive")
        .WhenFalse((_,evaluation) => $"the number is {evaluation.Assertions.Serialize()}")
        .Create();
````

### The `Spec<TModel>` type
This type is used to derive new types of _explanation_ specifications.
It is a syntactic sugar for the `Spec<TModel, string>` type.
```csharp
public class IsEvenSpec : Spec<int>(
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("number is even")
        .WhenFalse("number is odd")
        .Create());

var isEvenSpec = new IsEvenSpec();
```

### The `Spec<TModel, TMetadata>` type
This type is used to derive new types of _metadata_ specifications.
These specifications allow arbitrary types, known as _metadata_, to be attached to the result.
```csharp 

enum WithdrawalStatusCode
{
    SufficientFunds,
    InsufficientFunds
}

public class CanWithdrawCashSpec(decimal balance) : Spec<decimal, StatusCode>(
    Spec.Build((decimal n) => balance - withdrawal >= 0)
        .WhenTrue(WithdrawalStatusCode.SufficientFunds))
        .WhenFalse(WithdrawalStatusCode.InsufficientFunds)
        .Create("has sufficient funds"));

var canWithdrawCashSpec = new CanWithdrawCashSpec();
```

This can be useful when you want to provide additional information about the state of the model.