---
title: Spec
category: building
---

# Spec

While the <xref:Motiv.SpecBase`2> (or _specification_) is the base type for all  _propositions_, your direct
interactions with it will be through the `Spec` type., or one of its various generic versions.
For clarity, _specifications_ can be though of as the nodes in a logical syntax trees, with the tree itself being the
_proposition_.

1. Using the logical operators (such as `&`, `|`,  `^`etc.) to compose existing propositions.
2. Using the `Spec.Build()` method to create a new proposition.
3. Deriving from the `Spec<TModel>` or `Spec<TModel, TMetadata>` types to create a new proposition.

### Building propositions functionally

### `Spec`

```csharp
Spec.Build((int n) => n % 2 == 0)  // Spec used as static type
    .Create("is even");
````

This type is only used for building propositions using the logical operators.
As it is `static` it cannot be used to derive new types, but it can be used to compose new specifications.

### Policies

`PolicyBase<TModel, TMetadata>` are a type of proposition that derives from `SpecBase<TModel, TMetadata>`.
They are automatically created by using both `WhenTrue()` and `WhenFalse()` methods
