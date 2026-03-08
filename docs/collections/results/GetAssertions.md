# GetAssertions()

```csharp
IEnumerable<string> GetAssertions(this IEnumerable<BooleanResultBase> results)
```

The `GetAssertions()` extension method is used to extract the assertions from a collection of boolean results.

```csharp
IEnumerable<BooleanResult<int, string>> results = 
    [
        new IsEvenProposition().Evaluate(4),
        new IsGreaterThanProposition(5).Evaluate(6)
    ];

results.GetAssertions();  // [ "is even", "is greater than 5" ]
```