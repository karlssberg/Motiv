---
title: Create()
category: building
---

## Overview

The `Create()` method finalizes the specification building process, producing a `SpecBase<TModel, TMetadata>` instance that represents the proposition.

> [!IMPORTANT]
> **v8 breaking change:** Named explanation specs now report `name == true/false`; to keep string assertions, use parameterless `Create()`, or read the strings from `Values`.

## Default

```csharp
SpecBase<TModel, TMetadata> Create()
```

In the specific case of using the `WhenTrue(string assertion)` method, the `Create()` method can be called without
any arguments. This is because the propositional statement is obtained from the `WhenTrue()` method.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")  // The propositional statement
    .WhenFalse("is odd")
    .Create();            // no argument is required
```

## With an explicit propositional statement

```csharp
SpecBase<TModel, TMetadata> Create(string statement)
```

This method is used to create a new proposition with a given propositional statement.
The propositional statement is used when serializing the proposition to a string, such as with the propositions
`Description` property, or the results `Reason` property.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .Create("is even");
```

When `WhenTrue()`/`WhenFalse()` strings are also present, supplying an explicit name demotes those strings to metadata:
the name plus the `== true`/`== false` suffix becomes the `Reason`/`Assertions`/`Justification` text, and the strings
surface instead via `Values`.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")
    .WhenFalse("is odd")
    .Create("even check");
// true:  Assertions = ["even check == true"], Values = ["is even"]
// false: Assertions = ["even check == false"], Values = ["is odd"]
```

## With an implicit propositional statement

```csharp
SpecBase<TModel, TMetadata> Create()
```

When no argument is provided to `Create()`, the method uses a propositional statement that is inferred from the
previously called `WhenTrue()` method.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")  // This provides the implicit statement
    .WhenFalse("is odd")
    .Create();            // Uses "is even" implicitly
```

## As a policy

Policies are a type of proposition that derive from the `SpecBase<TModel, TMetadata>` class.
A policy is created when a proposition returns only a single assertion/metadata-object.

Policies are automatically created when both the `WhenTrue()` and `WhenFalse()` methods are used together.

```csharp
PolicyBase<int, string> isEvenPolicy =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("is even")
        .WhenFalse("is odd")
        .Create();
```

They can also be created when building an atomic proposition (instead of extending an existing one).

```csharp
PolicyBase<int, string> isEvenPolicy =
    Spec.Build((int n) => n % 2 == 0) // Atomic propositions take predicate functions
        .Create("is even");
```

## As a specification

When a proposition yields more than one assertion/metadata-object, it cannot be represented as a policy.
In such cases, a `SpecBase<TModel, TMetadata>` object is created instead of a `PolicyBase<TModel, TMetadata>` object.

```csharp
SpecBase<IEnumerable<int>, string> allPositiveSpec =
    Spec.Build((int n) => n > 0)
        .AllSatisfied()
        .WhenTrue("all positive")
        .WhenFalseYield(eval => eval.FalseModes.Select(n => $"{n} is not positive")) // prevents policy creation
        .Create();
```

## See Also

- [WhenTrue()](../assertions/WhenTrue.md)
- [WhenFalse()](../assertions/WhenFalse.md)
- [WhenFalseYield()](../assertions/WhenFalseYield.md)
