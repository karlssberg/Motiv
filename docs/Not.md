---
title: Not()
---
# Logical NOT `!`
You can perform a logical NOT operation by using the using the `!` operator `!spec`,
or alternatively using the method `proposition.Not()`.
This will produce a new proposition that is the logical NOT of the original.

For example:

```csharp
var isEvenSpec = Spec~~~~
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

<div style="display: flex; justify-content: space-between">
    <a href="./XOr.html">&lt; Previous</a>
    <a href="./Collections.html">Next &gt;</a>
</div>