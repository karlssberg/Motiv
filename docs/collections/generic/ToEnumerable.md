# ToEnumerable()

```csharp
IEnumerable<T> ToEnumerable<T>(this T value)
```

The `ToEnumerable()` extension method is used to encapsulate a single value as an `Enumerable<T>`
Internally, it yields a value instead of wrapping it in a collection data structure, and thus avoids an unnecessary
memory allocation.

```csharp
IEnumberable<int> value = 42.ToEnumerable();  // [ 42 ]
```