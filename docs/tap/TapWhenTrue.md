---
title: TapWhenTrue()
category: tap
---
# TapWhenTrue()

Wraps a <xref:Motiv.SpecBase`2> so that a callback fires only when the evaluation is satisfied. When the proposition is not satisfied, the callback is skipped.

```csharp
spec.TapWhenTrue((model, result) => { /* side-effect on success */ })
```

For example:

```csharp
var isEligible = Spec
    .Build((Customer c) => c.CreditScore > 600)
    .WhenTrue("eligible for loan")
    .WhenFalse("not eligible for loan")
    .Create();

var observed = isEligible.TapWhenTrue((customer, result) =>
    logger.LogInformation("Customer {Id} approved", customer.Id));

var result = observed.Evaluate(goodCustomer);
// Logger called: "Customer 42 approved"
result.Satisfied;  // true
result.Assertions; // ["eligible for loan"]

var result2 = observed.Evaluate(badCustomer);
// Logger NOT called
result2.Satisfied;  // false
result2.Assertions; // ["not eligible for loan"]
```

The tapped proposition is fully transparent — the result, description, and all output are identical to the original proposition regardless of whether the callback fires.
