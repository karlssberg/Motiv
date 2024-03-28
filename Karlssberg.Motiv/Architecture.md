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

### Spec Composition
For pragmatic reasons a couple of classes are provided (`Spec<TMode>` and `Spec<TModel,TMetadata>`) which can be 
used derive new custom types, but this is for the express purpose of encapsulating compositions of specifications, 
rather than defining new behaviour for the specification itself. These classes are to be used to encapsulate 
pre-defined compositions of specifications as new types, and whilst it is not strictly necessary to use these classes, 
they provide a compile-time representation that can be easily shared and re-used throughout an application.

### Explanation Specs
_Explanation_ specs are the lifeblood of this library and effectively a fallback when using specifications with 
differing metadata types. They are therefore guaranteed to have meaningful assertions. Depending on how the 
specification is constructed, the assertions may or may not be derived from the proposition or the `WhenTrue()` and 
`WhenFalse()` methods.

### Metadata Specs
_Metadata_ specs are a more advanced feature of this library and is used to provide arbitrary information about the 
specification evaluation.  Its primary purpose is to facilitate multilingual user feedback, but it is not 
limited to this use case.  There is a slight overlap in the roles of `Assertions` and metadata, but the key 
difference is that metadata will only be yielded by boolean results that share the same metadata type, whereas 
assertions will consider every boolean result, regardless of the metadata type.

### Boolean Results
Specifications, when evaluated, produce `BooleanResult<TMetadata>` instances. These maintain the tree structure of 
the specification composition so that the caller can interrogate the result at various levels of granularity. 
Results themselves can subsequently be used like native `bool` types.  This means that results from evaluations by 
specifications of differing model types can be combined using the `!`, `&`, `|`, and `^` operators, with their data
being correctly aggregated.