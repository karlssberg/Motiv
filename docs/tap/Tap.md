---
title: Tap()
category: tap
---
# Tap()

Wraps a <xref:Motiv.SpecBase`2> so that a callback fires on every evaluation, regardless of the outcome. The result is returned unchanged.

```csharp
spec.Tap((model, result) => { /* side-effect */ })
```

The callback receives both the model being evaluated and the <xref:Motiv.BooleanResultBase`1>, giving full access to `Satisfied`, `Reason`, `Assertions`, and `Values`.

For example:

```csharp
var isAdult = Spec
    .Build((Person p) => p.Age >= 18)
    .WhenTrue("is an adult")
    .WhenFalse("is not an adult")
    .Create();

var logged = isAdult.Tap((person, result) =>
    Console.WriteLine($"{person.Name}: {result.Reason}"));

var result = logged.IsSatisfiedBy(new Person("Alice", 25));
// Console output: "Alice: is an adult"
result.Satisfied;  // true
result.Assertions; // ["is an adult"]
```

The tapped proposition is fully transparent — `Description`, `Reason`, `Justification`, and `Assertions` are all identical to the original proposition.

### Chaining

Tap can be chained with other Tap variants and logical operators:

```csharp
var observed = spec
    .Tap((model, result) => metrics.RecordEvaluation(result.Satisfied))
    .TapWhenFalse((model, result) => alertService.Notify(model, result.Reason));
```
