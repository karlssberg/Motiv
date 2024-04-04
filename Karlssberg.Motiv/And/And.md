# Logical AND

A logical AND operation can be performed on two specifications using the `&` operator 
```leftSpec & rightSpec``` or alternatively using the And  method ```leftSpec.And(rightSpec)```. This will produce a new 
specification instance that is the logical AND of the two specifications.
When evaluated, both operands will be evaluated, regardless of the outcome of the left operand—in other words, it 
is not short-circuited.

For example:

```csharp
record Subscription(DateTime Start, DateTime End, bool IsCancelled);
var activeSubscription = new Subscription(DateTime.Today, DateTime.Today.AddDays(1), false);

var now = DateTime.Now;

var hasSubscriptionStartedSpec =
    Spec.Build((Subscription s) => s.Start < now)
        .WhenTrue("subscription has started")
        .WhenFalse("subscription has not started")
        .Create();

var hasSubscriptionNotEndedSpec =
    Spec.Build((Subscription s) => s.End >= now)
        .WhenTrue("subscription has not ended")
        .WhenFalse("subscription has ended")
        .Create();

var isActiveSubscriptionSpec = hasSubscriptionStartedSpec & hasSubscriptionNotEndedSpec;

var result = showShippingPageButton.IsSatisfiedBy(activeSubscription);

result.Satisfied; // true
result.Reason; // "subscription has started & subscription has not ended"
result.Assertions; // ["subscription has started", "subscription has not ended"]
```

The `Reason` property of the result will contain descriptions of the underlying causes of the result. If the results was
caused by both operands, then the `Reason` property will contain both assertions separated by the `&` operator to
indicate that both operands were responsible for the result, otherwise it will contain the single assertion that was
responsible.

```csharp
var result = isActiveSubscriptionResult.IsSatisfiedBy(lapsedSubscription);
result.Reason; // "subscription has ended"
```

If you want to give it a true or false reasons you can do so by wrapping it in a new `Spec<T>` instance.

```csharp
var isActiveSubscriptionSpec =
    Spec.Build(hasSubscriptionStarted & !hasSubscriptionEnded)
        .WhenTrue("subscription is active")
        .WhenFalse("subscription is not active")
        .Create();
```

You can also use the `&` operator on the `BooleanResult<T>`s that are returned from the `IsSatisfiedBy` method. This is
so that you can aggregate the results of multiple specifications of different models.

```csharp
var isValidLocationSpec =
    Spec.Build((Device device) => device.Country == Country.USA)
        .WhenTrue("device is permitted to play content within the USA")
        .WhenFalse("device not permitted to play content outside of the USA")
        .Create();

var isValidLocationResult = isValidLocationSpec.IsSatisfiedBy(device);
var isActiveSubscriptionResult = isActiveSubscriptionSpec.IsSatisfiedBy(subscription)

BooleanResultBase<string> canViewContent = isActiveSubscriptionResult & isValidLocationResult;
```

The results of the `&` operation being performed on two boolean results will be a new `BooleanResultBase<T>` 
instance that contains the results of the two.The `Result` property will therefore contain the assertions of both 
underlying specifications.

```csharp
var result = isActiveSubscriptionResult.IsSatisfiedBy(activeSubscription);
result.Reason; // "subscription has started & subscription has not ended"
```