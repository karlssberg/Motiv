# Minimal proposition
The most concise proposition can be created by providing a predicate and a propositional statement.
It uses the minimum set of fluent builder methods to create a proposition.

```csharp
var isEven =
    Spec.Build((int n) => n % 2 == 0)   // predicate
        .Create("is even");             // propositional statement

var result = isEven.IsSatisfiedBy(2);

result.Reason;    // "is even"
result.Assertion; // ["is even"]
```

And when negated:

```csharp
var result = isEven.IsSatisfiedBy(3);

result.Reason;    // "!is even"
result.Assertion; // ["!is even"]
```

It will implicitly create a `WhenTrue()` and `WhenFalse()` method, using as assertions the propositional statement 
provided to the `Create()` method, with the _false_ assertion being the negation of the propositional statement (prefixed with a `!`).

It is functionally equivalent to the following:

```csharp
Spec.Build((int n) => n % 2 == 0)   // predicate
    .WhenTrue("is even")            // propositional statement
    .WhenFalse("!is even")          // negation of the propositional statement
    .Create();
```

If the propositional statement contains an exclamation mark, parentheses or any other special character, then it 
will be encapsulated in parentheses (e.g. `!is even(-ish)` will become `(!is even(-ish))`) to the reader.

So using the example above:
```csharp
var isEven =
    Spec.Build((int n) => n % 2 == 0)  
        .WhenTrue("is even")           
        .WhenFalse("!is even")         
        .Create();

isEven.Description
```
