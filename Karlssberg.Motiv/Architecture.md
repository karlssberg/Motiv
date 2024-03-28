# Motiv Architecture

## Introduction
At the heart of Motiv is the Specification Pattern - a well established pattern for modelling domain propositions.  
As with everything in software development, there are inevitable trade-offs to be made when choosing the 
Specification pattern. These include extra verbose code, additional complexity when debugging, and the 
explainability of the results. This library aims to mitigate these trade-offs by providing a more succinct and API 
surface (by making it more functional than OO), providing detailed and reliable human-readable explanations for the 
results at various levels of granularity and depth.

## Key Features
### Fluent API
Where the original Specification pattern was soley focused encapsulating domain logic in a traditionally OO style, 
this library adopts a fluent API to create basic specifications.  The reason for adopting this approach was first to 
improve the developer experience. When instantiating classes using the `new` keyword you are forced to declare your 
generic parameters upfront. When this is coupled with an array of constructor overloads it can be very difficult to 
resolve signature violations. This made for a clumsy developer experience. The fluent API, on the other hand, allows 
you to exploit type inference to infer the generic parameters from the values supplied as method parameters. This 
makes for a much more intuitive developer experience, with less cognitive overhead, and any errors that do occur are 
much easier to diagnose since they are typically isolated to the values supplied as method parameters.

### Spec
The `Spec` class and its generic counterparts are the primary classes in this library. In order to ease the 
cognitive burden for the developer, the `Spec` classes were deliberately all named `Spec`, so in effect this is the 
cognitive entry-point for the developer. The genericless and static `Spec` class is used to fluently build new 
specifications, whereas the generic counterparts are used for spec composition.  This is not to be confused with the 
base classes `SpecBase<TModel>` and `SpecBase<TModel,TMetadata>`, which are used to provide a common public behaviors.

### Spec Composition
The main way specifications can be composed is by using the `&`, `|`, `^`, `!` operators (know in C# as the _boolean 
logical operators_). It's worth mentioning that _conditional boolean operators_, which are expressed as `&&`, `||`, 
are subtly different in that they are short-circuiting operators - meaning they will only evaluate the right operand 
if it cannot determine the final result from the left operand. The _boolean logical operators_ on the other hand 
are not short-circuiting and will therefore always evaluate both operands regardless.

Although the usage of this library is somewhat functional in nature, for pragmatic reasons a couple of classes are 
nonetheless provided for sub-typing (`Spec<TMode>` and `Spec<TModel,TMetadata>`). These are for the express purpose of 
encapsulating compositions of specifications, rather than defining new behaviour for the specification itself. In 
essence, they provide a compile time container for the specifications - otherwise you would only be able 
to work with instances of specifications, meaning re-use would be awkward.

### Explanation Specs
_Explanation_ specs are the lifeblood of this library and double as a fallback when using specifications with 
differing metadata types. They are therefore guaranteed to have meaningful `Assertions`. Depending on how the 
specification is constructed, the `Assertions` may or may not be derived from the proposition or the `WhenTrue()` and 
`WhenFalse()` methods (i.e. they may look like `"!is true"`).

### Metadata Specs
_Metadata_ specs are a more advanced use-case of this library and are used to provide arbitrary information about the 
specification evaluation.  Their _raison d'être_ is to facilitate multilingual user feedback, however, they are not 
limited to this single use case.

Metadata specs and explanation specs all share the same base types to simplify interoperability.  This means they 
share the same properties.  When working with strings it may appear that the `Assertions` property contains the same 
data as the `Metadata` property, but this is not strictly the case. There is an overlap in the roles that the 
`Assertions` and `Metadata` properties perform, but the key difference is that `Metadata` is an arbitrary type, and 
`Assertions` are always string.  Moreover, `Metadata` can only contain results obtained from underlying results that
share the same metadata-type, whereas `Assertions` will always contain meaningful information, regardless of the 
metadata type that happens to be in use.        

### Boolean Results
Specifications, when evaluated, produce `BooleanResultBase<TMetadata>` instances. These model results are each 
sub-expression in the underlying logical syntax tree.  This is so that the caller can inspect the underlying results at 
the various stages of evaluation (this avoids having to step through the code in a debugger. 

The `BooleanResultBase<TMetadata>` class is a generic class that is used to model the result of a specification instead 
of the native `bool` types.  This is so that we don't lose the associated underlying metadata that is generated 
during the evaluation. An important design consideration of this type was to forego having the model as a generic 
parameter. This means that results generated from different types of model can still be compared and metadata 
aggregated.  As such, the `BooleanResultBase<TMetadata>` class implements the `&`, `|`, `^`, and `!` operators (as 
well as the `==` and `!=` operators) to allow for the logical combination of these dissimilar results.

### Higher Order Logic
In order to perform logic over collections of models higher order logical operations are required. This library 
comes with with a few built-in higher order logical operations to cater for popular operations, but you can also add 
your own using extension methods and calling the `.As()` builder method.