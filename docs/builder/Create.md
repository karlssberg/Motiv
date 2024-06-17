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