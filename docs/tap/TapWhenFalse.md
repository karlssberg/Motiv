---
title: TapWhenFalse()
category: tap
---
# TapWhenFalse()

Wraps a <xref:Motiv.SpecBase`2> so that a callback fires only when the evaluation is not satisfied. When the proposition is satisfied, the callback is skipped.

```csharp
spec.TapWhenFalse((model, result) => { /* side-effect on failure */ })
```

For example:

```csharp
var isValid = Spec
    .Build((Order o) => o.Total > 0)
    .WhenTrue("order is valid")
    .WhenFalse("order has no items")
    .Create();

var observed = isValid.TapWhenFalse((order, result) =>
    logger.LogWarning("Invalid order {Id}: {Reason}", order.Id, result.Reason));

var result = observed.Evaluate(emptyOrder);
// Logger called: "Invalid order 7: order has no items"
result.Satisfied;  // false
result.Assertions; // ["order has no items"]

var result2 = observed.Evaluate(validOrder);
// Logger NOT called
result2.Satisfied;  // true
result2.Assertions; // ["order is valid"]
```

The tapped proposition is fully transparent — the result, description, and all output are identical to the original proposition regardless of whether the callback fires.
