# Spec Factory Decorator Proposition
A specification can be decorated with additional information using the `WhenTrue` and `WhenFalse` methods.
This is useful when you want to provide re-use, augment or override the underlying yielded results.

For example:

```csharp
var isEvenSpec =
    Spec.Build((int n) => n % 2 == 0)
        .Create("is even");

var isPositiveSpec = 
    Spec.Build(isEvenSpec)    
        .Create("is positive");
```

var isEvenBetterSpec = 
    Spec.Build(() =>
            {
                
            })      
        .WhenTrue("is even and positive")
        .WhenFalse(())
        .Create();
```