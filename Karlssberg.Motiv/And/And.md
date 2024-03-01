## AND operation

A logical AND operation can be performed on two specifications using the `&` operator.  This will produce a new
specification instance that is the logical AND of the two specifications.

```csharp
record Subscription(DateTime Start, DateTime End, bool IsCancelled);

var now = DateTime.Now;

var hasSubscriptionStarted = Spec
    .Build<Subscription>(s => s.Start < now)
    .WhenTrue("subscription has started")
    .WhenFalse("subscription has not started")
    .CreateSpec();

var hasSubscriptionEnded = Spec
    .Build<Subscription>(s => s.End < now)
    .WhenTrue("subscription has ended")
    .WhenFalse("subscription has not ended")
    .CreateSpec();

var isActiveSubscription = hasSubscriptionStarted & !hasSubscriptionEnded;

var subscription = new Subscription(now.AddDays(-1), now.AddDays(1), false);
var result = isActiveSubscription.IsSatisfiedBy(subscription);

result.Satisfied; // returns true
result.Reason; // returns "subscription has started & subscription has not ended"
result.Assertions; // returns ["subscription has started", "subscription has not ended"]
```

If you want to give it a true or false reasons you can do so by wrapping it in a new `Spec<T>` instance.
```csharp
var isActiveSubscription = Spec
    .Build<Subscription>(hasSubscriptionStarted & !hasSubscriptionEnded)
    .WhenTrue("subscription is active")
    .WhenFalse("subscription is not active")
    .CreateSpec();
```

You can also use the `&` operator on the `BooleanResult<T>`s that are returned from the `IsSatisfiedBy` method.  This is
so that you can aggregate the results of multiple specifications of different models.

```csharp
record Device(Country Country);

var isLocationUsa = Spec
    .Build<Device>(device => device.Country == Country.USA)
    .WhenTrue("the location is in the USA")
    .WhenFalse("the location is outside the USA")
    .CreateSpec();

var subscription = new Subscription(now.AddDays(-1), now.AddDays(1), false);
var device = new Device(Country.USA);

var canViewContent = isActiveSubscription.IsSatisfiedBy(subscription) & isLocationUsa.IsSatisfiedBy(device);

canViewContent.Satisfied; // returns true
canViewContent.Reason; // returns "subscription has started & subscription has not ended & the location is in the USA"
canViewContent.Assertions; // returns ["subscription has started", "subscription has not ended", "the location is in the USA"]
```

