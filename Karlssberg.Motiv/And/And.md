## AND operation

A logical AND operation can be performed on two specifications using the `&` operator or alternatively using
the `.And()` method. This will produce a new specification instance that is the logical AND of the two specifications.

```csharp
var now = DateTime.Now;

record Subscription(DateTime Start, DateTime End, bool IsCancelled);
var subscription = new Subscription(now.AddDays(-1), now.AddDays(1), false);

var hasSubscriptionStartedSpec = Spec
    .Build<Subscription>(s => s.Start < now)
    .WhenTrue("subscription has started")
    .WhenFalse("subscription has not started")
    .CreateSpec();

var hasSubscriptionNotEndedSpec = Spec
    .Build<Subscription>(s => s.End >= now)
    .WhenTrue("subscription has not ended")
    .WhenFalse("subscription has ended")
    .CreateSpec();

var isActiveSubscriptionSpec = hasSubscriptionStartedSpec & hasSubscriptionNotEndedSpec;
var result = isActiveSubscriptionSpec.IsSatisfiedBy(subscription);

result.Satisfied; // returns true
result.Reason; // returns "subscription has started & subscription has not ended"
result.Assertions; // returns ["subscription has started", "subscription has not ended"]
```

The `Reason` property of the result will contain descriptions of the underlying causes of the result. If the results was
caused by both operands, then the `Reason` property will contain both assertions separated by the `&` operator to
indicate that both operands were responsible for the result, otherwise it will contain the single assertion that was
responsible.

```csharp
var result = isActiveSubscriptionResult.IsSatisfiedBy(lapsedSubscription);
result.Reason; // returns "subscription has ended"
```

If you want to give it a true or false reasons you can do so by wrapping it in a new `Spec<T>` instance.

```csharp
var isActiveSubscriptionSpec = Spec
    .Build<Subscription>(hasSubscriptionStarted & !hasSubscriptionEnded)
    .WhenTrue("subscription is active")
    .WhenFalse("subscription is not active")
    .CreateSpec();
```

You can also use the `&` operator on the `BooleanResult<T>`s that are returned from the `IsSatisfiedBy` method. This is
so that you can aggregate the results of multiple specifications of different models.

```csharp
var isValidLocationSpec = Spec
    .Build<Device>(device => device.Country == Country.USA)
    .WhenTrue("device is permitted to play content within the USA")
    .WhenFalse("device not permitted to play content outside of the USA")
    .CreateSpec();

var isValidLocationResult = isValidLocationSpec.IsSatisfiedBy(device);
var isActiveSubscriptionResult = isActiveSubscriptionSpec.IsSatisfiedBy(subscription)

BooleanResultBase<string> canViewContent = isActiveSubscriptionResult & isValidLocationResult;
```

The results of the `&` operation being performed on two boolean results will be a new `BooleanResultBase<T>` instance that contains the results of the two.
The `Result`
property will therefore contain the assertions of both underlying specification.

```csharp
var result = isActiveSubscriptionResult.IsSatisfiedBy(activeSubscription);
result.Reason; // returns "subscription has started & subscription has not ended"
```