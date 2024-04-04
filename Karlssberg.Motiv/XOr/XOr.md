# Logical XOR

A logical XOR operation can be performed on two specifications using the `^` operator `left ^ right`,
or the alternative XOr method `left.XOr(right)`.
This will produce a new specification instance that is the logical XOR of the two specifications.

```csharp
record TrafficLight(Color Color);
var trafficLight = new TrafficLight(Color.Red);

var isRedLightSpec = Spec
    .Build((TrafficLight t) => t.Color == Color.Red)
    .WhenTrue("light is red")
    .WhenFalse("light is not red")
    .Create();

var isGreenLightSpec = Spec
    .Build((TrafficLight t) => t.Color == Color.Green)
    .WhenTrue("light is green")
    .WhenFalse("light is not green")
    .Create();

var isOperationalTrafficLightSpec = isRedLightSpec ^ isGreenLightSpec;
var isOperationalTrafficLight = isOperationalTrafficLightSpec.IsSatisfiedBy(trafficLight);

isTrafficLightFunctioning.Satisfied; // true
isTrafficLightFunctioning.Reason; // "light is red ^ light is not green"
isTrafficLightFunctioning.Assertions; // ["light is red", "light is not green"]
```

Notice that XOr will always output assertions for both underlying operands—in other words, it will always return 
two assertions (or possibly more it is used in conjunction with other specifications). This is because with the XOr 
operation it is not possible to determine the result of the operation without knowing the outcome of both operands = 
they are both causes.

If you want to give it a true or false reasons you can do so by wrapping it in a new specification.

```csharp
var isOperationalTrafficLightSpec = Spec
    .Build(isRedLightSpec ^ isGreenLightSpec)
    .WhenTrue("the traffic light is functioning correctly")
    .WhenFalse("the traffic light is faulty")
    .Create();
```

You can also use the `^` operator on the `BooleanResult<T>`s that are returned from the `IsSatisfiedBy` method. This is
so that you can still aggregate the results of specifications that interrogate different models.