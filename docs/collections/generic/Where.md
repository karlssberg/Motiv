# Where()

```csharp
IEnumerable<T> Where<T>(this IEnumerable<T> enumerable, SpecBase<TModel, TMetadata> spec)
```

The `Where()` extension method overload is used to filter a collection of values based on an existing specification.

```csharp
IEnumerable<int> evenNumbers = Enumerable
    .Range(1, 10)
    .Where(new IsEvenProposition());  // [ 2, 4, 6, 8, 10 ]
```

It serves as syntactic sugar for the following code:

```csharp
var isEven = new IsEvenProposition();
IEnumerable<int> evenNumbers = Enumerable
    .Range(1, 10)
    .Where(n => isEven.IsSatisfiedBy(n));  // [ 2, 4, 6, 8, 10 ]
```