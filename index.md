# Motiv

<div style="display: table; margin: auto">

```mermaid
flowchart BT

    A --> P(((&nbsp;Why Motiv?&nbsp;)))
    B([composable]) --> A((AND))
    C((AND)) --> A
    D([reusable]) --> C
    E([explainable]) --> C


    style E stroke:darkcyan
    style D stroke:darkcyan
    style B stroke:darkcyan
    style P stroke:darkcyan
```

</div>

### Know _Why_, not just _What_

---

Motiv is a developer-first .NET library that transforms the way you work with boolean logic.
It lets you form expressions from discrete [propositions](https://en.wikipedia.org/wiki/Proposition) so that you
can explain _why_ decisions were made.

To demonstrate Motiv in action,
let's create some [atomic propositions](https://en.wikipedia.org/wiki/Atomic_sentence):

```csharp
// Define propositions
var isValid = Spec.Build((int n) => n is >= 0 and <= 11).Create("valid");
var isEmpty = Spec.Build((int n) => n == 0).Create("empty");
var isFull  = Spec.Build((int n) => n == 11).Create("full");
```

Then compose using boolean operators:

```csharp
// Compose a new proposition
var isPartiallyFull = isValid & !(isEmpty | isFull);
```

And evaluate to get detailed feedback:

```csharp
// Evaluate the proposition
var result = isPartiallyFull.IsSatisfiedBy(5);

result.Satisfied;   // true
result.Assertions;  // ["valid", "¬empty", "¬full"]
result.Reason;      // "valid & !(¬empty | ¬full)"
```

## Installation

Motiv is available as a [NuGet Package](https://www.nuget.org/packages/Motiv/).
Install it using one of the following methods:

**NuGet Package Manager Console:**
```bash
Install-Package Motiv
```

**.NET CLI:**
```bash
dotnet add package Motiv
```

## Basic Usage


Let's start with an example of a minimal/atomic proposition to demonstrate Motiv's core concepts.  Take the example of
determining if a number is even:

```mermaid
flowchart BT

    True([is even]) -->|true| P(((is even?)))
    False([¬is even]) -->|false| P

    style P stroke:darkcyan
    style True stroke:darkgreen
    style False stroke:darkred
```

```csharp
// Define a atomic proposition
var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

// Evaluate the proposition
var result = isEven.IsSatisfiedBy(2);

result.Satisfied;   // true
result.Reason;      // "is even"
result.Assertions;  // ["is even"]
```

This minimal example showcases how easily you can create and evaluate propositions with Motiv.

## Advanced Usage

### Explicit Assertions

For more descriptive results, use `WhenTrue()` and `WhenFalse()` to define custom assertions.
Continuing with the previous example, let's provide more explicit feedback when the number is odd:

```mermaid
flowchart BT

    True([is even]) -->|true| P(((is even?)))
    False([is odd]) -->|false| P

    style P stroke:darkcyan
    style True stroke:darkgreen
    style False stroke:darkred
```

```csharp
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("is even")
        .WhenFalse("is odd")
        .Create();

var result = isEven.IsSatisfiedBy(3);

result.Satisfied;  // false
result.Reason;     // "is odd"
result.Assertions; // ["is odd"]
```

### Custom Metadata

For scenarios requiring more context, you can use metadata instead of simple string assertions.
For example, let's instead attach _metadata_ to our example:

```mermaid
flowchart BT

    True([new MyMetadata&lpar;&quot;is even&quot;&rpar;]) -->|true| P(((is even?)))
    False([new MyMetadata&lpar;&quot;is odd&quot;&rpar;]) -->|false| P

    style P stroke:darkcyan
    style True stroke:darkgreen
    style False stroke:darkred
```

```csharp
var isEven =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue(new MyMetadata("even"))
        .WhenFalse(new MyMetadata("odd"))
        .Create("is even");

var result = isEven.IsSatisfiedBy(2);

result.Satisfied;  // true
result.Reason;     // "is even"
result.Assertions; // ["is even"]
result.Metadata;   // [{ Text: "even" }]
```

### Composing Propositions

Motiv's true power shines when composing logic from simpler propositions, and then using their results to create new
assertions.
To demonstrate this,
we are going to solve the classic [Fizz Buzz](https://en.wikipedia.org/wiki/Fizz_buzz) problem using Motiv.
In this problem, we need to determine if a number is divisible by 3, 5, or both,
and then provide the appropriate feedback for each case.


Below is the flowchart of our solution:

```mermaid
flowchart BT
    TrueOr((OR)) -->|true| P(((is substitution?)))
    FalseOr([n]) -->|false| P
    TrueIsFizz(((fizz?))) -->|true| TrueOr
    TrueIsBuzz(((buzz?))) -->|true| TrueOr
    TrueIsFizzTrue([fizz]) -->|true| TrueIsFizz
    TrueIsBuzzTrue([buzz]) -->|true| TrueIsBuzz


    style P stroke:darkcyan
    style TrueOr stroke:darkgreen
    style TrueIsFizz stroke:darkgreen
    style TrueIsBuzz stroke:darkgreen
    style TrueIsFizzTrue stroke:darkgreen
    style TrueIsBuzzTrue stroke:darkgreen

    style FalseOr stroke:darkred
```

This is then implemented in code as follows:

```csharp
// Define atomic propositions
var isFizz = Spec.Build((int n) => n % 3 == 0).Create("fizz");
var isBuzz = Spec.Build((int n) => n % 5 == 0).Create("buzz");

// Compose atomic propositions and redefine assertions
var isSubstitution =
    Spec.Build(isFizz | isBuzz)
        .WhenTrue((_, result) => string.Concat(result.Assertions))  // Concatenate "fizz" and/or "buzz"
        .WhenFalse(n => n.ToString())
        .Create("is substitution");

isSubstitution.IsSatisfiedBy(15).Reason;  // "fizzbuzz"
isSubstitution.IsSatisfiedBy(3).Reason;   // "fizz"
isSubstitution.IsSatisfiedBy(5).Reason;   // "buzz"
isSubstitution.IsSatisfiedBy(2).Reason;   // "2"
```

This example demonstrates how you can compose complex propositions from simpler ones using Motiv.

---

## When to Use Motiv

Motiv is not meant to replace all your boolean logic.
You should only use it when it makes sense to do so.
If your logic is pretty straightforward or does not really need any feedback about the decisions being made, then
you might not see a big benefit from using Motiv.
It is just another tool in your toolbox, and sometimes the simplest solution is the best fit.

Consider using Motiv when you need two or more of the following:

1. **Visibility**: Granular, real-time feedback about decisions
2. **Decomposition**: Break down complex logic into meaningful subclauses
3. **Reusability**: Avoid logic duplication across your codebase
4. **Modeling**: Explicitly model your domain logic (e.g., for
   [Domain-Driven Design](https://en.wikipedia.org/wiki/Domain-driven_design))
5. **Testing**: Test your logic in isolation—without mocking dependencies

### Tradeoffs

1. **Performance**: Motiv is not designed for high-performance scenarios where every nanosecond counts.
   Its focus is on maintainability and readability, although in most use-cases the performance overhead is negligible.
2. **Dependency**: Once embedded in your codebase, removing Motiv can be challenging.
   However, it does not depend on any third-party libraries itself, so it won't bring any unexpected baggage.
3. **Learning Curve**: New users may need time to adapt to Motiv's approach and API
