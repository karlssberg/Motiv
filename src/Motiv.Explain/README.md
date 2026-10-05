# Motiv.Explain

Opt-in, compile-time decision logging for boolean methods. Mark a method with `[Explain]`, and builds that set
`MotivExplain=true` replace each direct call with a decomposed [Motiv](https://www.nuget.org/packages/Motiv)
specification that logs why it returned what it did. Every other build is left untouched: nothing is
generated, and the assembly doesn't reference Motiv.

```csharp
public static partial class Rules
{
    [Explain]
    public static bool IsEligible(Order o) =>
        o.Total > 100 && (o.Customer.IsActive || o.Customer.Age >= 65);
}
```

```
[motiv-explain] Shop.Rules.IsEligible(Shop.Order) => True
AND ALSO
    o.Total > 100 == true
    OR ELSE
        o.Customer.Age >= 65 == true
```

```xml
<PropertyGroup>
  <MotivExplain Condition="'$(Configuration)' == 'Debug'">true</MotivExplain>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Motiv.Explain" Version="..." PrivateAssets="all" />
</ItemGroup>
<ItemGroup Condition="'$(MotivExplain)' == 'true'">
  <PackageReference Include="Motiv" Version="..." />
</ItemGroup>
```

Requires the .NET 10 SDK. See the [documentation](https://karlssberg.github.io/Motiv/explain/index.html) for
the rules an explainable method must follow, where the logs go, and the calls that aren't explained.
