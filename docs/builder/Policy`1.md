### Creating explanation propositions as a policy class

### `Policy<TModel>`

```csharp
public class IsEvenProposition : Policy<int>( // Spec used as base type
    Spec.Build((int n) => n % 2 == 0)       // Spec used as static type
        .Create("is even"));

public class IsEvenAndPositiveProposition : Policy<int>(() => // Spec used as base type
    {
        var isEven = new IsEvenProposition();
        var isPositive = Spec.Build((int n) => n > 0)       // Spec used as static type
                             .Create("is positive");

        return Spec.Build(isEven & isPositive)
                   .WhenTrue("is even and positive")
                   .WhenFalse("is not even and positive")
                   .Create();
    });
```


This type is used to derive new types of _metadata_ policies that can be instantiated anywhere in your codebase.
It is a syntactic sugar for the `Policy<TModel, string>` type.
