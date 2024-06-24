---
title: And()
category: operators
---
# And()

### [Propositions](xref:Motiv.SpecBase`2)

You can perform a logical AND operation on two <xref:Motiv.SpecBase`2> in two ways:

* `left & right`
* `left.And(right)`

Both approaches produce a new proposition that represents the logical AND of the two input propositions.
When evaluating the resulting proposition, both operands will be evaluated,
regardless of the outcome of the left operand.
In other words, the logical AND operation is not short-circuited.

For example:

```csharp
var hasSubscriptionStarted =
    Spec.Build((Subscription s) => s.Start < DateTime.Now)
        .WhenTrue("subscription has started")
        .WhenFalse("subscription has not started")
        .Create();

var hasSubscriptionEnded =
    Spec.Build((Subscription s) => s.End < DateTime.Now)
        .WhenTrue("subscription has ended")
        .WhenFalse("subscription has not ended")
        .Create();

var isActiveSubscription = hasSubscriptionStarted & !hasSubscriptionEnded;

var result = isActiveSubscription.IsSatisfiedBy(activeSubscription);

result.Satisfied; // true
result.Reason; // "subscription has started & subscription has not ended"
result.Assertions; // ["subscription has started", "subscription has not ended"]
```
And when encountering an unsatisfied model:
```csharp
var result = isActiveSubscription.IsSatisfiedBy(inactiveSubscription);

result.Satisfied; // false
result.Reason; // "subscription has ended"
result.Assertion; // ["subscription has ended"]
```

The `Reason` property of the <xref:Motiv.BooleanResultBase`1> will contain descriptions of the underlying causes.
If the result was caused by both operands, then the `Reason` property will contain both assertions separated by the 
`&` operator to indicate that both operands were responsible for the result, otherwise it will contain the single 
assertion that was responsible.

If you want to give it a true or false reason, you can do so by building it as a new proposition.

For example:

```csharp
var isActiveSubscription =
    Spec.Build(hasSubscriptionStarted & !hasSubscriptionEnded)
        .WhenTrue("subscription is active")
        .WhenFalse("subscription is not active")
        .Create();
```

### [Boolean Results](xref:Motiv.BooleanResultBase`1)

You can perform a logical AND operation on two <xref:Motiv.BooleanResultBase`1> in two ways:

* `left & right`
* `left.And(right)`

This allows you to combine into a single result the evaluations of different model types (by different propositions).

For example:
```csharp
var isValidLocation =
    Spec.Build((Device device) => device.Country == Country.USA)
        .WhenTrue("device is permitted to play content within the USA")
        .WhenFalse("device not permitted to play content outside of the USA")
        .Create();

var canViewContent = 
    isValidLocation.IsSatisfiedBy(device) &
    isActiveSubscription.IsSatisfiedBy(subscription);
```

The results of the `&` operation being performed on two boolean results will be a new <xref:Motiv.BooleanResultBase`1>
instance that contains the results of the two.
The `Result` property will therefore contain the assertions of both underlying propositions.

```csharp
var result = isActiveSubscriptionResult.IsSatisfiedBy(activeSubscription);
result.Reason; // "subscription has started & subscription has not ended"
```