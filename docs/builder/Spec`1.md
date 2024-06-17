### Creating propositions as a class

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