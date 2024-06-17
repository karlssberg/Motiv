# Explicit propositional statements

When the `WhenTrue()` method takes a `string` you can it is automatically used as the *propositional statement*.

Alternatively, you can define the propositional statement using the `Create()` method.
This will populate the `Statement` property of <xref:Motiv.SpecBase>.

```csharp
var isNegative =
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create("is negative");

isNegative.Statement;  // "is negative"
``` 