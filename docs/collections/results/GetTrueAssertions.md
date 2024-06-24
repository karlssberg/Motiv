# GetTrueAssertions()

```csharp
IEnumerable<string> GetTrueAssertions(this IEnumerable<BooleanResultBase> results)
```

The `GetTrueAssertions()` extension method is used to extract the assertions from a collection of boolean results
that are satisfied.

```csharp
var left = Spec.Build((bool b) => b).Create("left");
var right = Spec.Build(bool b) => !b).Create("right");    

// XOR always considers both operands as causes, regardless of their results
var spec =
     Spec .Build(left ^ right)
              .WhenTrueYield((_, result) => result.Causes.GetTrueAssertions())
              .WhenFalse("none")
              .Create("xor");

spec.IsSatisfiedBy(true).Assertions;  // ["left"]
spec.IsSatisfiedBy(false).Assertions;  // ["right"]
```