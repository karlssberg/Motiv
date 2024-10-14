---
title: From()
---
# From()

The `Spec.From()` method is used to create a proposition from a lambda expression.
This is the easiest way to use Motiv, as it allows you to create multiple propositions from a single lambda expression.

For example, the following lambda expression:

```csharp
Spec.From((int n) => n > 1 & n < 10)
    .Create("n is between 1 and 10");
```

Internally, this example is transformed into two propositions: `n > 1` and `n < 10`.
Depending on their contribution to the final result, the sub-expressions could be filtered and/or changed to create
a meaningful explanation of the outcome.

So, with the expression `n > 1`, if `n` were `0` - and therefore unsatisfied - then the yielded assertions would be
`n <= 1`, which is the negation of the original expression.

For example:

```csharp
var inRange = Spec.From((int n) => n > 1 & n < 10)
                  .Create("n is between 1 and 10");

var result = inRange.IsSatisfiedBy(0);

result.Satisfied;  // false
result.Assertions; // ["n <= 1"]
```

## Displaying Values instead of Identifiers

The `Display.AsValue()` method can be used to replace identifiers with their actual values.
This allows for more informative assertions that are easier to understand.
In fact, the `Display.AsValue()` method can be used with any sub-expression (or the entire expression), if you so wish.

For example:

```csharp
var members = new[] { "Alice", "Ben", "Claudia" };
var isMember = Spec.From((string name) => members.Contains(Display.AsValue(name)))
                   .Create("is a member");

var result = isMember.IsSatisfiedBy("Ben");

result.Satisfied;  // true
result.Assertions; // ["members.Contains(\"Ben\") == true"]
```
## Using `WhenTrue()` and `WhenFalse()`

Like the `Spec.Build()` method, the `Spec.From()` method can also be used with the `WhenTrue()` and `WhenFalse()`
methods.

For example:

```csharp
var isEven = Spec.From((int n) => n % 2 == 0)
                 .WhenTrue("is even")
                 .WhenFalse("is odd")
                 .Create();

var result = isEven.IsSatisfiedBy(3);

result.Satisfied;     // false
result.Assertions;    // ["is odd"]
result.Justification; // is odd
                      //     n % 2 != 0
```

## Using Inline Propositions

Propositions can be used within the lambda expression of the `Spec.From()` method, and have their assertions and
metadata automatically incorporated into the final result.
This allows you to customize the assertions and metadata of the proposition, without having to create a separate

```csharp
var isAdmin =
    Spec.Build((string role) => role == "admin")
        .WhenTrue("is admin")
        .WhenFalse("is not admin")
        .Create();

var hasAccess = Spec
    .From((User user) => user.IsActive & user.Roles.Any(isAdmin))
    .Create("has admin access");
```
