## Creating a composite specification

A composite specification is a specification that is composed of other specifications. The composite specification can
be created by using the `&` operator, which is the logical AND operator. The `&` operator is overloaded for
the `Spec<T>` class. The `&` operator returns a new `Spec<T>` instance that represents the logical AND of the two
specifications.

```csharp
        var isPositive = new Spec<int>(
            n => n > 0,
            "the number is positive",
            n=> n == 0
                ? "the number is zero"
                : "the number is negative");
        
        var isEven = new Spec<int>(
            n => n % 2 == 0,
            "the number is even",
            "the number is odd");
        
        var isPositiveAndEven = isPositive & isEven;
        
        BooleanResultBase<string> booleanResult = isPositiveAndEven.IsSatisfiedBy(2);
        
        Console.WriteLine(booleanResult.IsSatisfied); // output is: true
        Console.WriteLine(string.Join(", ", booleanResult.Reasons)); // output is: the number is positive, the number is even
```

