---
title: Build()
---
# Build()

New propositions are built using the `Spec.Build()` method.
This method is overloaded and takes one of the following types of arguments:

| Name                                                                    | Type                                         | Description                                                           |
|-------------------------------------------------------------------------|----------------------------------------------|-----------------------------------------------------------------------|
| [Boolean Predicate](./Build.md#from-a-predicate-function)               | `Func<TModel, bool>`                         | a predicate that returns a boolean value.                             |
| [Boolean Result Predicate](./Build.md#from-the-result-of-a-proposition) | `Func<TModel, BooleanResultBase<TMetadata>>` | a predicate that returns a <xref:Motiv.BooleanResultBase`1> instance. |
| [Proposition](./Build.md#from-an-existing-proposition)                  | `SpecBase<TModel,TMetadata>`                 | a proposition that has already been built.                            |
| [Proposition Factory](./Build.md#from-a-proposition-factory)            | `Func<SpecBase<TModel, TMetadata>>`          | a factory function that returns a proposition.                        |

All of these overloads can be used to create a new proposition with varying levels of expressiveness.

### From a predicate function

```csharp
Build<TModel>(Func<TModel, bool> predicate)
```

Building using a predicate function is the canonical way of creating a proposition.
Propositions built from these generally serve as the foundations to more complex propositions.

```csharp
Spec.Build((int n) => n % 2 == 0) 
    .Create("is even"); 
```


### From the result of a proposition

```csharp
Build<TModel, TMetadata>(Func<TModel, BooleanResultBase<TMetadata>> pred~~ica~~te)
```

This is semantically the same as the previous example, but uses a <xref:Motiv.BooleanResultBase`1> to encapsulate the
result of the proposition, instead of a raw `bool`.
This would be typically generated by an evaluation of another predicate, but instantiating your own result object is
also possible.

```csharp
Spec.Build((int n) => new IsEvenProposition().IsSatisfiedBy(n))
    .Create("is even (better)");
```

### From an existing proposition

```csharp
Build<TModel, TMetadata>(SpecBase<TModel,TMetadata> proposition)
```

This is used to derive a proposition directly from another proposition.  It is commonly used to change the
assertions or metadata to a different value or type, as well as to compose new propositions as a one-line expression.

```csharp
Spec.Build(new IsEvenProposition())
    .Create("is even (better)");
```

### From a proposition factory

```csharp
Build<TModel, TMetadata>(Func<SpecBase<TModel, TMetadata>> factory)
```

This is used to create a proposition from a factory function.
The function is immediately invoked and the result is used to create the proposition.
This doesn't add any new capabilities, but is instead to assist with encapsulation and readability.

```csharp
Spec.Build(() => new IsEvenProposition())
    .Create("is even (better)");
```
