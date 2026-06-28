---
title: Tap
description: Documentation for Tap extension methods in Motiv that attach side-effects to propositions without altering their logical behavior.
---

# Tap

Motiv provides Tap extension methods for attaching side-effects to [propositions](xref:Motiv.SpecBase`2) without altering their logical behavior. This is useful for logging, metrics, debugging, or any other observation that should occur during evaluation.

A tapped proposition is fully transparent — its `Description`, `Reason`, `Assertions`, and all other output are identical to the original proposition. The tap simply fires a callback during `Evaluate` evaluation.

## Available Methods

| Method                                  | Description                                                                                                |
|-----------------------------------------|------------------------------------------------------------------------------------------------------------|
| [Tap()](Tap.md)                         | Fires a callback on every evaluation, regardless of the outcome.                                          |
| [TapWhenTrue()](TapWhenTrue.md)         | Fires a callback only when the proposition is satisfied.                                                   |
| [TapWhenFalse()](TapWhenFalse.md)       | Fires a callback only when the proposition is not satisfied.                                               |

## Example

```csharp
var isEligible = Spec
    .Build((Customer c) => c.CreditScore > 600)
    .WhenTrue("eligible")
    .WhenFalse("not eligible")
    .Create();

// Log every evaluation
var observed = isEligible
    .TapWhenTrue((customer, result) =>
        logger.LogInformation("Customer {Id} is eligible", customer.Id))
    .TapWhenFalse((customer, result) =>
        logger.LogWarning("Customer {Id} denied: {Reason}", customer.Id, result.Reason));

// Use exactly like the original proposition
var result = observed.Evaluate(customer);
result.Satisfied;   // true or false — unchanged
result.Assertions;  // ["eligible"] or ["not eligible"] — unchanged
```

## Key Characteristics

- **Transparent**: Tap does not appear in `Reason`, `Justification`, or `Description` output. The tapped proposition behaves identically to the original.
- **Composable**: Taps can be chained and combined with logical operators like any other proposition.
- **Evaluation-only**: Callbacks fire during `Evaluate` only. The lightweight `Matches` method (boolean-only path) does not trigger callbacks.
- **Returns Spec**: All Tap methods return `SpecBase<TModel, TMetadata>`, not `PolicyBase`. This is consistent with how most composition operations work in Motiv.

## Next Steps

- Explore the [Builder API](../builder/index.md) for creating propositions.
- Learn about [Operators](../operators/index.md) for combining propositions.
- Discover [Collections](../collections/index.md) extensions for working with multiple results.
