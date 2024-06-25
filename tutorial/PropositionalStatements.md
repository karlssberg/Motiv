# Propositional Statements

In Motiv, a *propositional statement* is a human-readable description of a proposition (before it is evaluated).

Propositional statements can be defined in two ways:

1. **Implicit** - by using the `WhenTrue(string assertion)` method.

    ```csharp
    var isNegative =
        Spec.Build((int n) => n < 0)
            .WhenTrue("negative")      // Implicit proposition
            .WhenFalse("not negative")
            .Create();

    isNegative.Statement;  // "negative"
    ```

2. **Explicit** - by using the `Create(string statement)` method.

    ```csharp
    var isNegative =
        Spec.Build((int n) => n < 0)
            .WhenTrue("negative")
            .WhenFalse("not negative")
            .Create("is negative");     // Explicit proposition

    isNegative.Statement;  // "is negative"
    ```
