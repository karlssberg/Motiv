### Creating explanation propositions as a spec class

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

This type is used to derive new types of _explanation_ specifications that can be instantiated anywhere in your
codebase.
It is a syntactic sugar for the `Spec<TModel, string>` type.
