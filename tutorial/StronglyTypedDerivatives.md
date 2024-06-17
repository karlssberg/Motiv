# Strongly Typed Derivatives

Whilst you can functionally redefine propositions based on an existing proposition, there may be
times when you want your proposition to be a strongly typed class so that it can be independently instantiated.
Motiv has some classes to inherit from that provide this functionality.
They ensure interoperability with existing propositions, so that you can still use the boolean operations to compose 
further propositions later on 

## <xref:Motiv.Spec`2>

By encapsulating a proposition in a <xref:Motiv.Spec`2> you make the proposition available as a unique type.

```csharp
public class IsEvenProposition : Spec<int, string>(
    Spec.Build((int n) => n % 2 == 0)
        .Create("is even"));
```

## <xref:Motiv.Spec`1>

For simplicity/readability reasons, Motiv provides a base class that defines a `string` as the default metadata type.

```csharp
public class IsEvenProposition : Spec<int>(
    Spec.Build((int n) => n % 2 == 0)
        .Create("is even"));
```