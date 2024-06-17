# GetAllRootAssertions()

```csharp
IEnumerable<string> GetAllRootAssertions(this IEnumerable<BooleanResultBase> results)
```

The `GetAllRootAssertions()` extension method is used to extract the root assertions from a collection of boolean
results, whether they cause the result to be satisfied or not.

```csharp
var isEven = 
    Spec.Build<int>(n => n % 2 == 0)
        .WhenTrue("is even")
        .WhenFalse("is odd")
        .Create();

var areEven =
    Spec.Build(isEven)
        .AsAnySatisfied()
        .WhenTrue("some even")
        .WhenFalse("all odd")
        .Create();

areEven.IsSatisfiedBy([ 1, 2, 3, 4 ]).GetAllRootAssertions();  // [ "is even", "is odd" ]
```