---
title: Compile-Time Explain
description: Motiv.Explain, an opt-in package that makes debug builds log why a boolean method returned what it did, by replacing each call with a decomposed Motiv specification at compile time, while leaving prod builds untouched.
---

`Motiv.Explain` lets a boolean method explain itself in the builds where you want explanations, and costs
nothing in the builds where you don't. Mark the method with `[Explain]`:

```csharp
public static partial class Rules
{
    [Explain]
    public static bool IsEligible(Order o) =>
        o.Total > 100 && (o.Customer.IsActive || o.Customer.Age >= 65);
}
```

In an *explain build*, every direct call to `IsEligible` evaluates the same expression as a decomposed Motiv
specification and logs the justification:

```
[motiv-explain] Shop.Rules.IsEligible(Shop.Order) => True
AND ALSO
    o.Total > 100 == true
    OR ELSE
        o.Customer.Age >= 65 == true
```

In every other build the attribute is inert. Calls bind to the original method, no Motiv code is generated,
and the assembly doesn't reference Motiv.

## Why compile time?

The decision is the same code in both builds: you write the expression once, as ordinary C#. What changes is
only whether the build adds the explanation, so production pays nothing for a debugging aid. This suits agents
and people diagnosing behaviour locally or in a test environment, where knowing *which clause decided* is
worth far more than the extra work.

## Setting it up

> [!NOTE]
> `Motiv.Explain` is not on NuGet yet. Like the rules-stack packages, it will get its own release train
> (`motiv-explain-v*` tags) before it ships. Until then, reference `src/Motiv.Explain` as an analyzer project
> and import its `build/Motiv.Explain.props` and `build/Motiv.Explain.targets`, as
> `test/Motiv.Explain.IntegrationTests` does.

Reference the package, decide which builds explain, and reference Motiv only in those builds:

```xml
<PropertyGroup>
  <MotivExplain Condition="'$(Configuration)' == 'Debug'">true</MotivExplain>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Motiv.Explain" Version="..." PrivateAssets="all" />
</ItemGroup>

<!-- Prod builds can't bind to Motiv, even by accident -->
<ItemGroup Condition="'$(MotivExplain)' == 'true'">
  <PackageReference Include="Motiv" Version="..." />
</ItemGroup>
```

`MotivExplain` can come from anywhere MSBuild reads properties, for example `dotnet test -p:MotivExplain=true`
in a test environment. When it is `true`, the package's targets also:

- add `Motiv.Explain.Generated`, the one namespace its interceptors live in, to `InterceptorsNamespaces`;
- define `MOTIV_EXPLAIN`, so code that configures logging can sit under `#if MOTIV_EXPLAIN`.

The package needs the .NET 10 SDK, in every build. Interceptors themselves are stable from the .NET 9.0.2xx
SDK, but the generator is built against Roslyn 5, which ships with .NET 10. An older SDK can't load it, and since
the generator is what supplies `[Explain]`, a prod build fails too.

## Where the explanations go

Each explained call goes to `MotivExplainLog.Sink`, which writes to `System.Diagnostics.Trace` by default.
Point it anywhere, or set it to `null` to stop logging:

```csharp
#if MOTIV_EXPLAIN
MotivExplainLog.Sink = decision =>
    logger.LogDebug("{Member} => {Satisfied}: {Reason}", decision.Member, decision.Satisfied, decision.Result.Reason);
#endif
```

An `ExplainedDecision` carries the method (`Member`), its answer (`Satisfied`) and the full Motiv result
(`Result`), so `Justification`, `Reason` and `Assertions` are all available. Because an explained call is an
ordinary Motiv evaluation, it also reports through [Observability](../observability/index.md) when something
subscribes to the `Motiv` source.

## Which methods can be explained

A method can be explained when it:

- returns `bool` from a single expression, either as an expression body or a single `return` statement;
- is declared in a class (or record class) that is `partial`, along with every type around it, and every
  type around it is a class too;
- isn't generic, isn't an extension method, has no `ref`, `out` or `in` parameters and no ref struct or pointer
  parameters, can't be overridden, and doesn't use `base`;
- doesn't read a primary constructor parameter, which the generated code can't reach, and doesn't only throw;
- has a signature the rest of the assembly can see, since the generated code sits outside the type.

A partial method is explained whichever half carries the attribute. The type has to be partial because the
decomposed specification is generated inside it, so its clauses can read the same private fields and methods the
method does. The analyzer reports any `[Explain]` method that
breaks these rules; see [Diagnostics](diagnostics.md).

## How the expression is split

The expression is split at `&&`, `||`, `&`, `|`, `^` and `!` wherever the operands are `bool`. Each piece
becomes its own clause, named by its source text, and the clauses are recombined with `AndAlso`, `OrElse`,
`And`, `Or`, `XOr` and `Not`. The evaluation order and short-circuiting are the same as the original
expression's.

A clause is an ordinary delegate, so unlike `Spec.From` it can contain anything a lambda can: patterns, switch
expressions and `?.` included. Two pieces stay together when the second reads a variable the first declares,
as in `o.Customer is { } c && c.Age >= 65`, since they could no longer share a scope.

## What is not explained

An explain build replaces *call sites*, not the method itself. These calls still run the original method and
log nothing:

- a call through a delegate or method group, such as `orders.Where(Rules.IsEligible)` (reported as MOTIV1001);
- a call from another project (reported as MOTIV1002);
- a call through an interface or a base-class reference, which binds to a different method;
- a call made from inside another explained method's clauses, which run as generated code;
- a call inside an expression tree, including a query over `IQueryable`, so a provider that reads the tree
  (EF Core, for example) still finds the original method there.

## How prod stays untouched

When `MotivExplain` isn't `true`, the generator returns before producing anything but the attribute class. The
attribute is `[Conditional("MOTIV_EXPLAIN")]`, so its usages aren't even written to the prod assembly's
metadata. With Motiv referenced only in explain builds, a prod build has nothing it could call. A CI check that
fails when a Release assembly references `Motiv` turns this into a hard guarantee.
