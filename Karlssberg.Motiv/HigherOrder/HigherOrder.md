# Higher Order Logic

The propositions we have mentioned thus far are known as first-order propositions since they apply to a single entity.
However, this is incomplete since it does not propose the state of a set of entities.
Higher order propositions allow you to promote a first order proposition (or a regular predicate) to its higher order 
equivalent, effectively allowing you to make a determination about the state of a set of models.

Because there are an unlimited number of ways to describe the composition of a set, you are given the flexibility to
make determinations about the set in arbitrary ways.  The library provides an `As()` method that allows you to specify 
a higher-order predicate, and optionally a collection of results that should be considered causal/determinative.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .As((results) => results.AllTrue())
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
