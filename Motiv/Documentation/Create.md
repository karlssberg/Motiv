﻿# Create()

## Creating propositions
### `Create(string statement)`
```csharp
Spec.Build((int n) => n % 2 == 0)
    .Create("is even");
```
This method is used to create a new proposition with a given propositional statement.
The propositional statement is used when serializing the proposition to a string, such as with the propositions 
`Description` property, or the results `Reason` property.

## Creating concise explanation propositions 
### `Create()`
```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")    // The propositional statement is obtained here
    .WhenFalse("is odd")
    .Create();              // no argument is required
```
In the specific case of using the `WhenTrue(string assertion)` method, the `Create()` method can be called without 
any arguments. This is because the propositional statement is obtained from the `WhenTrue()` method.

<div style="display: flex; justify-content: left;">
  <a href="./.md">Back - When False</a>
</div>

| [Back - _WhenFalse()_](./WhenFalse.md) |
|:--------------------------------------:|