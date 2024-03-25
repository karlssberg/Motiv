# The Spec type

This library is designed to be as flexible as possible. This means that it supports define your own custom 
type that can instantiated and re-used throughout your application.  It does this by providing two generic type called 
`Spec<TModel>` and `Spec<TModel, TMetadat>`. The `Spec<TModel>` type is used to define specifications that yield 
strings that assert the state of a model. The `Spec<TModel, TMetadat>` type is used to define specifications that 
yield custom POCO objects that can either be used to provide a more detailed explanation of the result (such as in 
different languages), or to provide additional information about the state of the model.

### The `Spec<TModel>` type
```csharp
public class IsEvenSpec : Spec<int>(
    Spec.Build<int>(n => n % 2 == 0)
        .WhenTrue("number is even")
        .WhenFalse("number is odd")
        .Create());

var isEvenSpec = new IsEvenSpec();
```

### The `Spec<TModel, TMetadata>` type
```csharp
public class CanWithdrawMoney : Spec<decimal, decimal>(
    Spec.Build<decimal>(n => n > 0)
        .WhenTrue(n => n)
        .WhenFalse(0)
        .Create("can withdraw money"));

var canWithdrawMoneySpec = new CanWithdrawMoneySpec();
```

This can be useful when you want to provide additional information about the state of the model. For example,

```csharp
public class EqualsSpec<TModel>(TMode value) : Spec<TModel>(
    Spec.Build<TModel>(model => model.Equals(value))
        .WhenTrue("model equals value")
        .WhenFalse("model does not equal value")
        .Create());
```