# AllFalse()

```csharp
bool AllFalse<TMetadata>(this IEnumerable<BooleanResultBase<TMetadata>> results)
```
The `AllFalse()` extension method is used to determine whether all the boolean results are not satisfied.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().IsSatisfiedBy(1),
        new IsGreaterThanProposition(5).IsSatisfiedBy(1)
    ];

results.AllFalse();  // true
```