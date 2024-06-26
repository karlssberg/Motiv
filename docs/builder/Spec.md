---
title: Spec
category: building
---

# Spec

While the <xref:Motiv.SpecBase`2> (or _specification_) is the base type for all  _propositions_, your direct 
interactions with it will be through the `Spec` type., or one of its various generic versions.
For clarity, _specifications_ can be though of as the nodes in a logical syntax trees, with the tree itself being the 
_proposition_.

1. Using the logical operators (such as `&`, `|`,  `^`etc.) to compose existing propositions.
2. Using the `Spec.Build()` method to create a new proposition.
3. Deriving from the `Spec<TModel>` or `Spec<TModel, TMetadata>` types to create a new proposition.

### Building propositions functionally

### `Spec` 

```csharp
Spec.Build((int n) => n % 2 == 0)  // Spec used as static type
    .Create("is even");
````

This type is only used for building propositions using the logical operators.
As it is `static` it cannot be used to derive new types, but it can be used to compose new specifications.

### `Spec<TModel, TMetadata>`

This type is used to derive new types of _metadata_ specifications.
These specifications allow arbitrary types, known as _metadata_, to be attached to the result.

```csharp 
public class IsEvenProposition : Spec<int, MyMetadata>( // Spec used as base type
    Spec.Build((int n) => n % 2 == 0)                   // Spec used as static type
        .WhenTrue(new MyMetadata("is even"))
        .WhenFalse(new MyMetadata("is odd"))
        .Create("is even"));

public class IsEvenAndPositiveProposition : Spec<int, MyMetadata>(() => // Spec used as base type
    {
        var isEven = new IsEvenProposition();
        var isPositive = 
            Spec.Build((int n) => n > 0)                                // Spec used as static type
                .WhenTrue(new MyMetadata("is positive"))
                .WhenFalse(new MyMetadata("is negative or zero"))
                .Create("is positive");
        
        return isEven & isPositive;    
    });
```

This can be useful when you want to provide additional information about the state of the model.