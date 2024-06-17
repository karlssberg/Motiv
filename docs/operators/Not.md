---
title: Not()
category: operators
---
# Not()

### [Propositions](xref:Motiv.SpecBase`2)

You can perform a logical NOT operation on a <xref:Motiv.SpecBase`2> in two ways:

* `!proposition`
* `proposition.Not()`

This will produce a new proposition that is the logical NOT of the original.

For example:

```csharp
var isEvenSpec = Spec
    .Build((int n) => n % 2 == 0)
    .WhenTrue("even")
    .WhenFalse("odd")
    .Create();

var isOddSpec = !isEvenSpec; // same as: isEvenSpec.Not()

var isOdd = isOddSpec.IsSatisfiedBy(3);
isOdd.Satisfied; // true
isOdd.Reason; // "odd"
isOdd.Assertions; // ["odd"]
```

### [Boolean Results](xref:Motiv.BooleanResultBase`1)

You can perform a logical NOT operation on a <xref:Motiv.BooleanResultBase`1> in two ways:

* `!proposition`
* `proposition.Not()`
