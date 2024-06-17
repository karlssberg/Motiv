# Multiple assertions

Sometimes you will want to generate multiple assertion statements.  This is done using the `WhenTrueYield()` and the
`WhenFalseYield()` methods.  These methods take a factory function that returns an `IEnumerable<string>`.

```csharp
var allEven =
    Spec.Build((int n) => n % 2 == 0))
        .AsAllSatisfied()
        .WhenTrue("all are even")
        .WhenFalseYield(eval => eval.FalseModels.Select(n => $"{n} is odd"))
        .Create();

var results = allEven.Evaluate(1, 2, 3, 4, 5);

result.Assertions; // ["1 is odd", "3 is odd", "5 is odd"]
```