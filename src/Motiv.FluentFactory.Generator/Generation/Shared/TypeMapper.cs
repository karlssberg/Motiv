using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Motiv.FluentFactory.Generator.Generation.Shared;

public static class TypeMapper
{
    public static ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> MapGenericArguments(ITypeSymbol openType, ITypeSymbol closedType)
    {
        var typeMap = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        MapTypes(openType, closedType, typeMap);
        return typeMap.ToImmutableDictionary(SymbolEqualityComparer.Default);
    }

    private static void MapTypes(ITypeSymbol open, ITypeSymbol closed, Dictionary<ITypeParameterSymbol, ITypeSymbol> typeMap)
    {
        switch (open, closed)
        {
            case (ITypeParameterSymbol typeParam, _):
                typeMap[typeParam] = closed;
                break;
            case (INamedTypeSymbol openNamed,  INamedTypeSymbol closedNamed)
                when openNamed.TypeArguments.Length == closedNamed.TypeArguments.Length:
            {
                for (var i = 0; i < openNamed.TypeArguments.Length; i++)
                {
                    MapTypes(openNamed.TypeArguments[i], closedNamed.TypeArguments[i], typeMap);
                }
                break;
            }
        }
    }

    public static IMethodSymbol NormalizeMethodTypeParameters(
        this IMethodSymbol method,
        ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> substitutions)
    {
        // If no type parameters, return as-is
        if (!method.IsGenericMethod)
            return method;

        // Construct new method type
        var typeArgs = method.TypeArguments
            .Select(arg =>
            {
                if (arg is not ITypeParameterSymbol typeArg)
                    return arg;

                return substitutions.TryGetValue(typeArg, out var replacement)
                    ? replacement
                    : typeArg;
            });

        return method.Construct(typeArgs.ToArray());
    }
}
