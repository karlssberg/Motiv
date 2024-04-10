# Propositions

In logic, a proposition is a declarative statement that can be either _true_ or _false_.
In the context of this library, a proposition is formed from one or more specifications, by using the `Spec.Build()`
method.
In most cases, the terms _proposition_ and _specification_ are used interchangeably, but there is a subtle 
difference.
Logical _propositions_ are what we are trying to model here, whereas _specifications_ are the building blocks with 
which we achieve this.

Or to put it another way:

>_specifications are to propositions as words are to sentences_.

A proposition can be constructed by using the fluent builder methods available on the `Spec` class.
They accept predicates (e.g. `Func<TModel, bool>`) and existing specifications which can be used to form the 
foundations of a more complex proposition.
Propositions can also be formed from combining existing propositions using the `&`, `|`, and `^` operators, which 
itself can be wrapped in a new proposition and supplied a high-level reason for when either the result is true or 
false.
The caller can nonetheless interrogate the underlying reasons for the result to provide a detailed breakdown of 
the decision.
A few different types of propositions are available, but one thing they all have in common is the ability to provide 
an explanation/information about the decision.
The form this information takes is entirely left up to the user, but it is typically a string that describes the result.

## Creating a proposition
A proposition can be built by using the `Spec.Build()` method.
This method is overloaded and takes either a regular predicate (i.e. `Func<TModel, bool>`), a specification (i.e. 
`Spec<TModel,TMetadata>`), a specification-predicate (i.e. `Func<TModel, Spec<TModel,TMetdata>>`), or a specification 
factory (i.e. `Func<Spec<TModel, TMetadata>>`.
All of these overloads can be used to create a new proposition with varying levels of expressiveness.

### Minimal proposition
The most terse proposition can be created by providing a predicate and a propositional statement.
```csharp
Spec.Build((int n) => n % 2 == 0)
    .Create("is even");
```
In this case, when the predicate is true, the proposition is satisfied and the reason given is `"even"`.
When the predicate is false, the proposition is not satisfied and the reason given is `"!even"`.

Whilst this is useful for debugging purposes (and surfacing to logicians), it is not suitable for most end users.  
Therefore, there are builder methods that can be used to provide human-readable reasons for when either the result is 
true or false.
```csharp
Spec.Build((User user) => user.IsActive)
    .WhenTrue(user => $"{user.Name} is active")
    .WhenFalse(user => $"{user.Name} is not active")
    .Create();
```
In this case, when the predicate is true, the proposition is satisfied and the reason given is `"number is even"`. 
When the predicate is false, the proposition is not satisfied and the reason given is `"number is odd"`.
However, you may wish to describe some aspect of the input value in the reason.
This can be done by providing a function.
```csharp
Spec.Build((int n)n => n % 2 == 0)
    .WhenTrue(n => $"{n} is even")
    .WhenFalse(n => $"{n} is odd")
    .Create();
```
### Advanced usage
There may be times when a string of text describing the result is not enough.
For example, you may want to present the text to an international audience.
In this case, you can provide a custom object for `.WhenTrue()` and `.WhenFalse()` instead of using a string.
```csharp
Spec.Build((Product product) => product.InStock)
    .WhenTrue(new { English = $"{product.Name} is in stock", Spanish = $"{product.Name} está en stock" })
    .WhenFalse(new { English = $"{product.Name} is out of stock", Spanish = $"{product.Name} está agotado" })
    .Create($"is {product.Name} stock in stock");
```
Notice that here you have to provide a string argument for the `Create()` method.
This is because it is not clear to the library what the proposition is about.
It is therefore necessary to solicit this form the caller to ensure that it is still possible to provide a 
meaningful explanation. 
This does however mean that the 'Reason' may contain a `!`, but in this scenario the caller is not expected to 
surface explanation to the user and instead use the `Metadata` property to custom information about the outcome.

```csharp
var isProductInStock =
    Spec.Build((Product product) => product.InStock)
        .WhenTrue(new { English = $"{product.Name} is in stock", Spanish = $"{product.Name} está en stock" })
        .WhenFalse(new { English = $"{product.Name} is out of stock", Spanish = $"{product.Name} está agotado" })
        .Create($"is {product.Name} stock in stock");

var isProductInStock = isEvenSpec.IsSatisfiedBy(new Product("Television"));
isEven.Satisfied; // false
isEven.Reason; // "!Television is in stock"
isEven.Assertions; // ["!Television is in stock"]
isEven.Metadata.Select(m => m.English); // ["Television is out of stock"]
```