# Spec

Instances of the `Spec` type are the building blocks of a _propositions_.
They are the nodes of a logical syntax trees that collectively we call _propositions_.
There are three main ways to create a proposition.

1. Using the logical operators (such as `&`, `|`,  `^`etc.) to compose existing propositions.
2. Using the `Spec.Build()` method to create a new proposition.
3. Deriving from the `Spec<TModel>` or `Spec<TModel, TMetadata>` types to create a new proposition.

### Building propositions

### `Spec` 

```csharp
Spec.Build((int n) => n % 2 == 0)  // Spec used as static type
    .Create("is even");
````

This type is only used for building propositions using the logical operators.
As it is `static` it cannot be used to derive new types, but it can be used to compose new specifications.

### Creating strongly typed propositions

### `Spec<TModel>`

```csharp
public class IsEvenProposition : Spec<int>( // Spec used as base type
    Spec.Build((int n) => n % 2 == 0)       // Spec used as static type
        .Create("is even"));

public class IsEvenAndPositiveProposition : Spec<int>(() => // Spec used as base type
    {
        var isEven = new IsEvenProposition();
        var isPositive = Spec.Build((int n) => n > 0)       // Spec used as static type
                             .Create("is positive");
        
        return isEven & isPositive;    
    });
```

This type is used to derive new types of _explanation_ specifications.
It is a syntactic sugar for the `Spec<TModel, string>` type.
The primary constructor accepts an

### `Spec<TModel, TMetadata>`

This type is used to derive new types of _metadata_ specifications.
These specifi[WhenTrueYield.md](WhenTrueYield.md)cations allow arbitrary types, known as _metadata_, to be attached to the result.

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

<div style="display: flex; justify-content: right;">
    <a href="./Build.html">Next &gt;</a>
</div>