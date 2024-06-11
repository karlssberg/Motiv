# WhenTrue()

The `WhenTrue()` method is used to yield values when the proposition is satisfied. It also implicitly sets `TMetadata`
type for the rest of the proposition. Any values yielded from underlying propositions (if they exist) will be supplanted
by the new value from the
`WhenTrue()` method. However, should the underlying yielded values still be required, then it is still possible to
re-yield them.

Whilst the `WhenTrue()` method overloads remains broadly consistent across the various ways a proposition can be built,
there are nuances to be aware of that are as a result of prior builder method calls. The outliers are the Higher-Order
propositions, which require the pairing of models and results to be preserved for them to be useful.

### Fixed assertion

### `.WhenTrue(string assertion)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")            // "is even" is also used as the propositional statement
    .WhenFalse("is odd")
    .Create();                      // parameter not required - propositional statement is already provided
```

This overload is unique in that the value it takes can also be used as the propositional statement. This means that you
can use the parameterless `Create()` method fo finalize the building of the proposition
(although you can still use the `Create(string statement)` method if you wish). When the proposition is satisfied, the
assertion value will be used to populate the `Reason`, `Assertions` and
`Metadata` properties of the result.

### Fixed metadata

### `.WhenTrue(TMetadata metadata)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse(new MyMetadata("is odd"))
    .Create("is even");
```

This overload sets the metadata for the proposition when it is satisfied. It works the same as the previous example, but
with a non-string metadata object. This overload requires that the `Create()` method be called with a string parameter
to set the propositional statement. When the proposition is satisfied, the metadata will be used to populate
the `Metadata` property of the result.

### Dynamic assertion (derived from model)

### `.WhenTrue(Func<TModel, string> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue(n => $"{n} is even")
    .WhenFalse("is odd")
    .Create("is even");
```

This overload generates assertion statements based on the model when the proposition is satisfied. When the proposition
is satisfied, the assertion result from the factory function will be used to populate the
`Reason`, `Assertions` and `Metadata` properties of the result.

### Dynamic metadata (derived from model)

### `.WhenTrue(Func<TModel, TMetadata> factory)`

```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue(n => new MyMetadata($"{n} is even"))
    .WhenFalse(new MyMetadata("is odd"))
    .Create("is even");
```

This overload generates metadata based on the model when the proposition is satisfied. When the proposition is
satisfied, the metadata returned by the factory function will be used to populate the
`Metadata` property of the result.

### Dynamic assertion (derived from model and underlying result)

### `.WhenTrue(Func<TModel, BooleanResultBase<TMetadata>, string> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue((n, result) => result.Reason)
    .WhenFalse("is odd")
    .Create("is even");
```

This overload generates assertion statements based on the model and the result of the underlying proposition. When the
proposition is satisfied, the assertion result from the factory function will be used to populate the
`Reason`, `Assertions` and `Metadata` properties of the result.

### Dynamic assertion (derived from model and underlying result)

### `.WhenTrue(Func<TModel, BooleanResultBase<TMetadata>, TMetadata> factory)`

```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue((n, result) => new MyMetadata($"{n} {result.Reason}"))
    .WhenFalse(new MyMetadata("is odd"))
    .Create("is even");
```

This overload generates a metadata value based on the model and the result of the underlying proposition. When the
proposition is satisfied, the metadata returned by the factory function will populate the `Metadata` property of the
result.

| [Back - _As()_](./As.md) | [Next - _WhenTrueYield()_](./WhenTrueYield.md) |
|:------------------------:|:----------------------------------------------:|