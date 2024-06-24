# GetRootAssertions()

```csharp
IEnumerable<string> GetRootAssertions(this BooleanResultBase result)
```

The `GetRootAssertions()` extension method is used to extract the assertions from the root causes.
Only the root assertions that caused the final result to be satisfied or not are returned.

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


areEven.IsSatisfiedBy([ 1, 2, 3, 4 ]).GetRootAssertions();  // [ "is even" ]
```