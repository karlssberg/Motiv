### Creating metadata propositions as a spec class

### `Policy<TModel, TMetadata>`

```csharp
public class IsEvenProposition : Policy<int, MyMetadata>( // Spec used as base type
    Spec.Build((int n) => n % 2 == 0)       // Spec used as static type
        .WhenTrue(new MyMetadata("is even"))
        .WhenFalse(new MyMetadata("is odd"))
        .Create("is even"));

public class IsEvenAndPositiveProposition : Policy<int, MyMetadata>(() => // Spec used as base type
    {
        var isEven = new IsEvenProposition();
        var isPositive = Spec.Build((int n) => n > 0)       // Spec used as static type
                             .WhenTrue(new MyMetadata("is positive"))
                             .WhenFalse(new MyMetadata("is negative"))
                             .Create("is positive");

        return Spec.Build(isEven & isPositive)
                   .WhenTrue(new MyMetadata("is even and positive"))
                   .WhenFalse(new MyMetadata("is not even and positive"))
                   .Create("is even and positive");
    });
```

This type is used to derive new types of _metadata_ policies that can be instantiated anywhere in your codebase.
