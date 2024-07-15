# Explanation Propositions

```mermaid
%%{init: { 'themeVariables': { 'fontSize': '13px' }}}%%
flowchart BT
    True(["&quot;true assertion&quot;"]) -->|true| P((is satisfied?))
    False(["&quot;false assertion&quot;"]) -->|false| P

    style P stroke:darkcyan,stroke-width:4px
    style True stroke:darkgreen,stroke-width:2px
    style False stroke:darkred,stroke-width:2px
```


_Explanation propositions_ are used to provide human-readable feedback to users (or developers).

While all propositions will populate the `Assertions` property with useful information, _explanation propositions_
populate `Assertions` with the strings obtained from their `WhenTrue()`, `WhenTrueYield()`, `WhenFalse()`, and
`WhenFalseYield()` methods.

For example, consider the following proposition:

```csharp
var isUserActive =
    Spec.Build((User user) => user.IsActive)
        .WhenTrue("user is active")
        .WhenFalse("user is not active")
        .Create();

isUserActive.Statement;  // "user is active"
isUserActive.Expression; // "user is active"

isUserActive.IsSatisfiedBy(activeUser).Reason;      // "user is active"
isUserActive.IsSatisfiedBy(activeUser).Assertion;   // ["user is active"]

isUserActive.IsSatisfiedBy(inactiveUser).Reason;    // "user is not active"
isUserActive.IsSatisfiedBy(inactiveUser).Assertion; // ["user is not active"]

```

