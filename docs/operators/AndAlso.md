---
title: AndAlso()
category: operators
---
# AndAlso()

### [Propositions](xref:Motiv.SpecBase`2)

You can perform a conditional AND (i.e., short-circuited) operation on two <xref:Motiv.SpecBase`2> in only one way:

* `left.AndAlso(right)`

This is due to quirks regarding the overloading of the `&&` operator, only the `AndAlso()` method is
available for use with propositions.

The conditional AND will produce a new proposition that represents the logical AND of the two input propositions.
When evaluating the resulting proposition, the right operand will only be evaluated if the left is satisfied.

For example:

```csharp
var emptyBasket = new Basket(Array.Empty<BasketItem>());
var isBasketEmpty =
    Spec.Build((Basket b) => b.Items.Count == 0)
        .WhenTrue("basket is empty")
        .WhenFalse(o => $"basket contains {o.Items.Count} items")
        .Create();

var isFreeShipping = 
    Spec.Build((Basket b) => b.Items.All(i => i.FreeShipping))
        .WhenTrue("free shipping")
        .WhenFalse("shipping payment required")
        .Create();

var chooseShippingOptions = (!isBasketEmpty).AndAlso(!isFreeShipping);

var result = chooseShippingOptions.IsSatisfiedBy(emptyBasket);

result.Satisfied; // false
result.Reason; // "basket is empty"
result.Assertions; // ["basket is empty"]
```

The `Reason` property of the result will contain human-readable descriptions of the causes.
If the results were caused by both operands, then the `Reason` property will contain both assertions separated by the 
`&&` operator to indicate that both operands were responsible for the result, otherwise it will contain the single 
assertion that was responsible.

```csharp
var result = isActiveSubscription.IsSatisfiedBy(lapsedSubscription);

result.Reason; // "subscription has ended"
```

If you want to give it a true or false reason, you can do so by building it as a new proposition.

For example:

```csharp
var isActiveSubscription =
    Spec.Build(hasSubscriptionStarted.AndAlso(!hasSubscriptionEnded))
        .WhenTrue("subscription is active")
        .WhenFalse("subscription is not active")
        .Create();
```

### [Boolean Results](xref:Motiv.BooleanResultBase`1)

You can perform a conditional AND operation on two <xref:Motiv.BooleanResultBase`1> in two ways:

* `left && right`
* `left.AndAlso(right)`

This allows you to combine into a single result the evaluations of different model types (by different propositions).

```csharp
var isValidLocation =
    Spec.Build((Device device) => device.Country == Country.USA)
        .Create();

var isValidLocationResult = isValidLocation.IsSatisfiedBy(device);
var isActiveSubscriptionResult = isActiveSubscription.IsSatisfiedBy(subscription)

BooleanResultBase<string> canViewContent = isActiveSubscriptionResult.AndAlso(isValidLocationResult);
```

The results of the `AndAlso()` operation being performed on two boolean results will be a new <xref:Motiv.BooleanResultBase`1>
instance that contains the results of the two.
The `Result` property will therefore contain the assertions of both underlying propositions.

```csharp
var result = isActiveSubscription.IsSatisfiedBy(activeSubscription);
result.Reason; // "subscription has started && subscription has not ended"
```