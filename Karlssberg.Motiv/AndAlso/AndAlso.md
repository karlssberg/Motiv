# Conditional AND

A conditional AND (i.e., short-circuited) operation can be performed on two specifications using the
`leftProposition.AndAlso(rightProposition)` method.
It is serialized as `leftProposition && rightProposition`—incidentally the `&&` C# operator cannot be overriden, so 
we have to resort to using method invocation to perform the operation.
This will produce a new specification instance that is the logical AND of the two specifications.
When evaluated, the right specification will only be evaluated if the left specification is satisfied.

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
```csharp
var isUserLoggedIn = new Spec<UserPrin>(true);
```
The `Reason` property of the result will contain human-readable descriptions of the causes.
If the results were caused by both operands, then the `Reason` property will contain both assertions separated by the 
`&&` operator to indicate that both operands were responsible for the result, otherwise it will contain the single 
assertion that was responsible.

```csharp
var result = isActiveSubscription.IsSatisfiedBy(lapsedSubscription);
result.Reason; // "subscription has ended"
```

If you want to give it a true or false reasons you can do so by wrapping it in a new `Spec<T>` instance.

```csharp
var isActiveSubscription =
    Spec.Build(hasSubscriptionStarted.AndAlso(!hasSubscriptionEnded))
        .WhenTrue("subscription is active")
        .WhenFalse("subscription is not active")
        .Create();
```

You can also use the `AndAlso()` operator on the `BooleanResult<T>`s that are returned from the `IsSatisfiedBy` 
method. This is so that you can aggregate the results of multiple specifications of different models.

```csharp
var isValidLocation =
    Spec.Build((Device device) => device.Country == Country.USA)
        .WhenTrue("device is permitted to play content within the USA")
        .WhenFalse("device not permitted to play content outside of the USA")
        .Create();

var isValidLocationResult = isValidLocation.IsSatisfiedBy(device);
var isActiveSubscriptionResult = isActiveSubscription.IsSatisfiedBy(subscription)

BooleanResultBase<string> canViewContent = isActiveSubscriptionResult.AndAlso(isValidLocationResult);
```

The results of the `AndAlso()` operation being performed on two boolean results will be a new `BooleanResultBase<T>` 
instance that contains the results of the two.
The `Result` property will therefore contain the assertions of both underlying specifications.

```csharp
var result = isActiveSubscription.IsSatisfiedBy(activeSubscription);
result.Reason; // "subscription has started & subscription has not ended"
```