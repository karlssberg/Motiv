---
title: Create()
category: building
---
# Create()

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

## With an implicit propositional statement

```csharp
SpecBase<TModel, TMetadata> Create()
```

This method is used to create a new proposition with a propositional statement that is inferred from the
`WhenTrue()` method.

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")
    .WhenFalse("is odd")
    .Create();
```

## As a policy

Policies are a type of proposition that derive from the `SpecBase<TModel, TMetadata>` class.
They are created when a proposition only returns a single assertion/metadata-object.

They will be created when both the `WhenTrue()` and `WhenFalse()` methods are used together.

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

Sometimes it is not possible to create a policy because the proposition yields more than one assertion/metadata-object.
In this case, a `SpecBase<TModel, TMetadata>` object is created instead of a `PolicyBase<TModel, TMetadata>` object.

```csharp
SpecBase<IEnumerable<int>, string> allPositiveSpec =
    Spec.Build((int n) => n > 0)
        .AllSatisfied()
        .WhenTrue("all positive")
        .WhenFalseYield(eval => eval.FalseModes.Select(n => $"{n} is not positive")) // prevents policy creation
        .Create();
```
