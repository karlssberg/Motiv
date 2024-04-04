# Specifications
Specifications combine to form logical syntax trees, which we call _propositions_.
There are three main ways to create a specification, one is by using the logical operators (such as `&`, `|`,  `^` 
etc.) another is by using the `Spec.Build()` method, and lastly by deriving from the `Spec<TModel>` or `Spec<TModel, 
TMetadata>` types (where the `Spec<TModel>` type is a syntactic sugar for the `Spec<TModel, string>` type).

### The `Spec` type
This type is only used for building propositions using the logical operators. As it is `static` it cannot be used to 
derive new types, but it can be used to compose new specifications.
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
        .WhenFalse((_,evaluation) => $"the number is {string.Join(" and ", evaluation.Assertions)}")
        .Create();
````

### The `Spec<TModel>` type
```csharp
public class IsEvenSpec : Spec<int>(
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("number is even")
        .WhenFalse("number is odd")
        .Create());

var isEvenSpec = new IsEvenSpec();
```

### The `Spec<TModel, TMetadata>` type
```csharp
public class CanWithdrawMoney : Spec<decimal, decimal>(
    Spec.Build((decimal n) => n > 0)
        .WhenTrue(n => n)
        .WhenFalse(0)
        .Create("can withdraw money"));

var canWithdrawMoneySpec = new CanWithdrawMoneySpec();
```

This can be useful when you want to provide additional information about the state of the model. For example,

```csharp
public class EqualsSpec<TModel>(TMode value) : Spec<TModel>(
    Spec.Build((TModel model) => model.Equals(value))
        .WhenTrue("model equals value")
        .WhenFalse("model does not equal value")
        .Create());
```