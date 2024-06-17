# Motiv Architecture

## Introduction
At the heart of Motiv is the Specification Pattern—a well-established pattern for modelling domain propositions.
As with everything in software development, there are inevitable trade-offs to be made when choosing any technical 
approach, and the same is true with the Specification pattern.
These include extra verbose code, additional complexity when debugging, and the explainability of the results.
We aim to mitigate these trade-offs by providing a succinct and simplified API surface, providing detailed and 
reliable human-readable explanations for the results at various levels of granularity and depth.

## Key Features
### Fluent and Functional
Whereas the original Specification pattern was focused on encapsulating domain logic in a more traditional OO style, 
we have adopted a functional approach with a fluent API to create the initial propositions.
The reason for adopting this approach was first and foremost to improve the developer experience.
Exhaustively defining every proposition as a class and having to consider a perplexing array of constructor overload 
in the base class quickly becomes an onerous task for the developer.
Furthermore, when consuming the classes using the `new` keyword we are forced to declare our generic type-parameters 
upfront, and ensure they are compatible with the values we supply.
This can be a source of frustration for developers when there are many overloads to contend with.
By adopting a fluent API, we can sidestep this annoyance by leveraging type inference.
This makes for a much more intuitive developer experience, with less cognitive overhead, and any type-errors that do 
occur are much easier to diagnose since they are typically isolated to a specific builder method.

### Spec
The `Spec` class and its generic counterparts are the primary classes that developers will use to create propositions.
To simplify the API surface area of Motiv, the `Spec` classes were all uniformly named _Spec_.
Not only does it serve as a convenient cognitive starting point for building propositions, but it is easy to type and
accurately alludes to it being an implementation of the _specification pattern_.
The latter is important because it is a known pattern in the software development community, and for those that are 
familiar with it, it will be immediately clear how Motiv generally works.
Whilst the generic-less `Spec` class is used to fluently build new propositions, the generic `Spec` classes serve as 
base classes for defining strongly typed propositions.
This results in propositions being treated as first-class citizens, meaning that they can be easily re-used and 
integrated, such as with dependency injection frameworks.

### Composition
The main way propositions can be composed is by using the `&`, `|`, `^`, operators (know in C# as the _boolean 
logical operators_) as well as the methods `AndAlso()` and `OrElse()`.
It's worth mentioning that _conditional boolean operators_, which are expressed as `&&`, `||`, 
are subtly different in that they are short-circuiting operators - meaning it will not bother to evaluate the right 
operand if the left operand is enough to determine the outcome.
The _boolean logical operators_, on the other hand, are not short-circuiting and will therefore always evaluate both 
operands regardless.
Although Motiv supports the use of the _conditional boolean operators_ using the `AndAlso()` and `OrElse()` methods, 
for the operators `&&` and `||` to participate, the two operands need to be immediately resolvable using the `true()`
or `false()` operator overloads.
Since propositions do not have this available while they are being constructed, it prevents the use of the `&&` 
and `||` operators by them.
This is not the case with <xref:Motiv.BooleanResultBase`1> instances, which can be composed using the _conditional
boolean operators_ `&&` and `||`.

In most cases you will want to use the _boolean logical operators_ over the _conditional boolean operators_ 
since they ensure that all propositions are evaluated, and therefore the explanations will be complete.
However, there may be occasions to use the _conditional boolean operators_, such as to filter out sanity checks
after they are successful.

Although Motiv is mainly used functionally, for pragmatic reasons a couple of classes are 
nonetheless provided for sub-typing (<xref:Motiv.Spec`1> and <xref:Motiv.Spec`2>).
These are for the express purpose of encapsulating compositions of propositions, rather than defining new behaviour 
for the proposition itself (which is still technically possible).
In essence, they allow us to give propositions a strongly typed name - otherwise we would only be able to work with
object instances, meaning re-use would be awkward.

### Explanation
Propositions yielding _explanations_ are the main use-case of this library and double as a fallback when using 
propositions with differing metadata types (to ensure explanations are as through as possible).
They are therefore guaranteed to have meaningful and complete `Assertions`.
In the absence of assertions being explicit defined by the builder, the `Assertions` property will be derived from
the _propositional statement_ supplied to the `Create()` method.

### Metadata
Propositions yielding _Metadata_ are a more advanced use-case of this library and are used to provide arbitrary 
information about the proposition evaluation.
Their _raison d'être_ is to facilitate multilingual user feedback; however, they are not limited to this single use 
case.

Metadata and explanation propositions all share the same base types to simplify interoperability.
This means they share the same properties.
When working with strings, it may appear that the `Assertions` property contains the same data as the `Metadata` 
property, but this is not strictly the case.
There is an overlap in the roles that the `Assertions` and `Metadata` properties perform, but the key difference is 
that `Metadata` is an arbitrary type, and `Assertions` are always string.
Moreover, `Metadata` can only contain results obtained from underlying results that share the same metadata-type, 
whereas `Assertions` will always contain meaningful information, regardless of the metadata type that happens to be 
in use.        

### Boolean Results
Propositions, when evaluated, produce <xref:Motiv.BooleanResultBase`1> instances.
These model results are each sub-expression in the underlying logical syntax tree.
This is so that the caller can inspect the underlying results at the various stages of evaluation (this avoids 
having to step through the code in a debugger. 

The <xref:Motiv.BooleanResultBase`1> class is a generic class used to model the result of a proposition instead 
of the native `bool` types.
This is so that we don't lose the associated underlying metadata generated during the evaluation.
An important design consideration of this type was to forego having the model as a generic parameter.
This means that results generated from different types of models can still be compared and metadata aggregated.
As such, the <xref:Motiv.BooleanResultBase`1> class implements the `&`, `|`, `^`, and `!` operators to allow for the 
logical combination of these dissimilar results, or when evaluations occur at different points in time, but the 
results require further logical operations to be performed upon them.

### Higher Order Logic
To perform logic over collections of models, higher order logical operations are required.
This library comes with a few built-in higher order logical operations to cater for popular operations, but you 
can also add your own using extension methods and calling the `.As()` builder method.