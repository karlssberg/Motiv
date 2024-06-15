---
title: Create()
category: building
---
## Default 

`SpecBase<TModel, TMetadata> Create()`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")  // The propositional statement
    .WhenFalse("is odd")
    .Create();            // no argument is required
```

In the specific case of using the `WhenTrue(string assertion)` method, the `Create()` method can be called without 
any arguments. This is because the propositional statement is obtained from the `WhenTrue()` method.

## With an explicit propositional statement

`SpecBase<TModel, TMetadata> Create(string statement)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .Create("is even");
```

This method is used to create a new proposition with a given propositional statement.
The propositional statement is used when serializing the proposition to a string, such as with the propositions
`Description` property, or the results `Reason` property.

<div style="display: flex; justify-content: space-between">
    <a href="./WhenFalseYield.html">&lt; Previous</a>
    <a href="./And.html">Next &gt;</a>
</div>