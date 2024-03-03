# XOR operation

A logical XOR operation can be performed on two specifications using the `^` operator. This will produce a new
specification instance that is the logical XOR of the two specifications.

```csharp
record TrafficLight(Color Color);
var trafficLight = new TrafficLight(Color.Red);

var isRedLightSpec = Spec
    .Build<TrafficLight>(t => t.Color == Color.Red)
    .WhenTrue("light is red")
    .WhenFalse("light is not red")
    .CreateSpec();

var isGreenLightSpec = Spec
    .Build<TrafficLight>(t => t.Color == Color.Green)
    .WhenTrue("light is green")
    .WhenFalse("light is not green")
    .CreateSpec();

var isOperationalTrafficLightSpec = isRedLightSpec ^ isGreenLightSpec;
var isOperationalTrafficLight = isOperationalTrafficLightSpec.IsSatisfiedBy(trafficLight);

isTrafficLightFunctioning.Satisfied; // returns true
isTrafficLightFunctioning.Reason; // returns "light is red ^ light is not green"
isTrafficLightFunctioning.Assertions; // returns ["light is red", "light is not green"]
```

If you want to give it a true or false reasons you can do so by wrapping it in a new specification.

```csharp
var isOperationalTrafficLightSpec = Spec
    .Build<TrafficLight>(isRedLightSpec ^ isGreenLightSpec)
    .WhenTrue("the traffic light is functioning correctly")
    .WhenFalse("the traffic light is faulty")
    .CreateSpec();
```

You can also use the `^` operator on the `BooleanResult<T>`s that are returned from the `IsSatisfiedBy` method. This is
so that you can still aggregate the results of specifications that interrogate different models.