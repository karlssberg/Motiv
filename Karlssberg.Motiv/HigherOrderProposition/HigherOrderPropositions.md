# Higher Order Propositions

The propositions we have mentioned thus far are known as first-order propositions since they apply to a single entity.
However, this is incomplete since it does not propose the state of a set of entities.
Higher order propositions allow you to promote a first order proposition (or a regular predicate) to its higher order 
equivalent, effectively allowing you to make a determination about the state of a set of models.

Because there are an unlimited number of ways to describe the composition of a set, you are given the flexibility to
make determinations about the set in arbitrary ways.  The library provides an `As()` method that allows you to specify 
a higher-order predicate, and optionally a collection of results that should be considered causal/determinative.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .As((results) => results.AllTrue())  // custom higher order predicate
    .WhenTrue("all are even")
    .WhenFalse("some are odd")
    .Create();
```

There are some built-in higher-order predicates that cater for the most common types of higher-order 
operations.

The current built-in higher order logical operations are:
- `AsAllSatisfied()`: Creates a proposition that yields a true boolean-result object if all the models in a
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAnySatisfied()`: Creates a proposition that yields a true boolean-result object if any of the models in a
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsNoneSatisfied()`: Creates a proposition that yields a true boolean-result object if none of the models in a
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsNSatisfied()`: Creates a proposition that yields a true boolean-result object if exactly N models in a
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAtLeastNSatisfied()`: Creates a proposition that yields a true boolean-result object if at least N models in a
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAtMostNSatisfied()`: Creates a proposition that yields a true boolean-result object if at most N models in a
  collection satisfy the proposition, otherwise a false boolean-result object is returned.

## Custom explanations and metadata

In real-world scenarios, you will probably want to describe edge cases in a more detailed way. Higher order 
operations will most likely require special language about empty or full sets, regardless of the logic being 
evaluated.  To assist with this, there is a _result_ object that captures the state of the evaluation and has 
convenient properties that work seamlessly with pattern matching.

```csharp
var allAreNegative =
    Spec.Build(new IsNegativeIntegerSpec())
        .AsAllSatisfied()                       // switch to higher order proposition
        .WhenTrue(eval => eval switch
        {
            { Count: 0 } => "there is an absence of numbers",
            { Models: [< 0 and var n] } => $"{n} is negative and is the only number",
            _ => "all are negative numbers"
        })
        .WhenFalse(eval => eval switch
        {
            { Models: [0] } => ["the number is 0 and is the only number"],
            { Models: [> 0 and var n] } => [$"{n} is positive and is the only number"],
            { NoneSatisfied: true } when eval.Models.All(m => m is 0) => ["all are 0"],
            { NoneSatisfied: true } when eval.Models.All(m => m > 0) => ["all are positive numbers"],
            { NoneSatisfied: true } =>  ["none are negative numbers"],
            _ => eval.FalseModels.Select(n => n is 0
                    ? "0 is neither positive or negative"
                    : $"{n} is positive")
        })
        .Create("all are negative");
```

#### Higher order output
Higher order propositions have more nuanced demands on the output.
To sweeten the syntactic sugar, the evaluation object presents a set of properties to make it easier to pattern match 
the underlying state. 

For example:
```csharp 
var allAreNegative =
    Spec.Build(new IsNegativeIntegerSpec())
        .AsAllSatisfied()
        .WhenTrue(eval => eval switch
        {
            { Count: 0 } => "there is an absence of numbers",
            { Models: [< 0 and var n] } => $"{n} is negative and is the only number",
            _ => "all are negative numbers"
        })
        .WhenFalse(eval => eval switch
        {
            { Models: [0] } => ["the number is 0 and is the only number"],
            { Models: [> 0 and var n] } => [$"{n} is positive and is the only number"],
            { NoneSatisfied: true } when eval.Models.All(m => m is 0) => ["all are 0"],
            { NoneSatisfied: true } when eval.Models.All(m => m > 0) => ["all are positive numbers"],
            { NoneSatisfied: true } =>  ["none are negative numbers"],
            _ => eval.FalseModels.Select(n => n is 0
                    ? "0 is neither positive or negative"
                    : $"{n} is positive")
        })
        .Create("all are negative");

allAreNegative.IsSatisfiedBy([]).Assertions; // ["there is an absence of numbers"]
allAreNegative.IsSatisfiedBy([-10]).Assertions; // ["-10 is negative and is the only number"]
allAreNegative.IsSatisfiedBy([-2, -4, -6, -8]).Assertions; // ["all are negative numbers"]
allAreNegative.IsSatisfiedBy([0]).Assertions; // ["the number is 0 and is the only number"]
allAreNegative.IsSatisfiedBy([11]).Assertions; // ["11 is positive and is the only number"]
allAreNegative.IsSatisfiedBy([0, 0, 0, 0]).Assertions; // ["all are 0"]
allAreNegative.IsSatisfiedBy([2, 4, 6, 8]).Assertions; // ["all are positive numbers"]
allAreNegative.IsSatisfiedBy([0, 1, 2, 3]).Assertions; // ["none are negative numbers"]
allAreNegative.IsSatisfiedBy([-2, -4, 0, 9]).Assertions; // ["9 is positive", "0 is neither positive or negative"]
```