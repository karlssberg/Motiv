# Propositions

In logic, a proposition is a statement that can be either true or false. In the context of this library, a 
proposition is represented as a specification that is either satisfied (true) or not satisfied (false).

A proposition can be constructed by using the fluent builder methods available on the `Spec` class. It accepts 
predicates (i.e. `Func<TModel, bool>`) which can be used to form the foundations of a complex proposition.  Propositions
can also be formed from combining existing specifications using the `&`, `|`, and `^` operators, which itself can be 
wrapped in a new specification and supplied a high-level reason for when either the result is true or false.  The 
underlying reasons for the result can nonetheless be interrogated by the caller to provide a detailed breakdown of 
the decision.  A few different types of propositions are available, but one thing they all have in common is the 
ability to provide an explanation/information about the decision. The form this information takes is entirely left 
up to the user, but it is typically a string that describes the result.

## Creating a proposition

A proposition can be built by using the `Spec.Build()` method. This method is overloaded and takes either a regular 
predicate (i.e. `Func<TModel, bool>`), a specification (i.e. `Spec<TModel,TMetadata>`), a specification-predicate
(i.e. `Func<TModel, Spec<TModel,TMetdata>>`), or a specification factory (i.e. `Func<Spec<TModel, TMetadata>>`.  All 
of these overloads can be used to create a new proposition with varying levels of expressiveness.

### Basic proposition
The most basic proposition can be created by providing a predicate and a propositional statement.
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .CreateSpec("even");
```
In this case, when the predicate is true, the proposition is satisfied and the reason given is `"even"`. When the 
predicate is false, the proposition is not satisfied and the reason given is `"!even"`.

Whilst this is useful for debugging purposes (and surfacing to logicians), it is not suitable for most end users.  
Therefore, there are builder methods that can be used to provide human-readable reasons for when either the result is 
true or false.
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue("number is even")
    .WhenFalse("number is odd")
    .CreateSpec();
```
In this case, when the predicate is true, the proposition is satisfied and the reason given is `"number is even"`. 
When the predicate is false, the proposition is not satisfied and the reason given is `"number is odd"`. However, 
you may wish to describe some aspect of the input value in the reason. This can be done by providing a function.
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue(n => $"{n} is even")
    .WhenFalse(n => $"{n} is odd")
    .CreateSpec();
```
### Advanced proposition
There may be times when a string of text describing the result is not enough. For example, you may want to present 
the text to an international audience. In this case, you can provide a custom object for `.WhenTrue()` and `.WhenFalse()`
instead of using a string.
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue(new { English = "the number is even", Spanish = "el número es par" })
    .WhenFalse(new { English = "the number is odd", Spanish = "el número es impar" })
    .CreateSpec("is even number");
```
Notice that here you have to provide a string argument for the `CreateSpec()` method. This is because it is not 
clear to the library what the proposition is about. It is therefore necessary to solicit this form the caller to 
ensure that it is still possible to provide a meaningful explanation. This does however mean that the explanation may 
contain a `!`, but in this scenario the caller is not expected to surface explanation to the user and instead use the 
`Metadata` property to custom information about the outcome.
```csharp
var isEvenSpec = Spec
    .Build<int>(n => n % 2 == 0)
    .WhenTrue(new { English = "the number is even", Spanish = "el número es par" })
    .WhenFalse(new { English = "the number is odd", Spanish = "el número es impar" })
    .CreateSpec("even");

var isEven = isEvenSpec.IsSatisfiedBy(3);
isEven.Satisfied; // returns true
isEven.Reason; // returns "!even"
isEven.Metadata.Select(m => m.English); // returns ["the number is odd"]
```

### Higher order propositions
From an mathematical viewpoint, higher order logic operations are applied to sets of propositions rather than 
individual (first order) propositions (they can also be applied to functions, but that is not relevant here). This 
library 
supports higher order logic by allowing you to promote a first order specification to a higher order equivalent.  
This means that instead of accepting a single model, it accepts a set of models from which it generates a set of 
results which are evaluated to determine if the higher order specification is satisfied.
```csharp
var isEven = Spec
    .Build<int>(n => n % 2 == 0)
    .CreateSpec("even");

var allAreEven = Spec
    .Build(isEven)
    .AsAllSatisfied()
    .CreateSpec("all are even");
```

#### Higher order output
When it come to higher-order propositions you will almost certainly want to describe how the set is composed in a 
multitude of ways.  This library aims to support this by providing a result object that contains convenient properties 
that assist with pattern matching. 
```csharp 
var allAreEven = Spec
    .Build(isEven)
    .AsAllSatisfied()
    .WhenTrue(result => {
        { 
            { TrueCount: > 1 } => "all are true",
            _ => "only one is true"
        })
    .WhenFalse(result =>
        result switch
        {
            { NoneSatisfied: true } => "none of the models are true",
            { FalseCount: 1 } => "only one model caused the proposition to be false",
            _ => $"{result.CausalResults.Count} models caused the proposition to be false"
        })
    .CreateSpec();
```