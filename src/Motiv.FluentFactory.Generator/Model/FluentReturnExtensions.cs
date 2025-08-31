using Microsoft.CodeAnalysis;
using Motiv.FluentFactory.Generator.Generation;
using Motiv.FluentFactory.Generator.Model.Steps;

namespace Motiv.FluentFactory.Generator.Model;

public static class FluentReturnExtensions
{
    public static IEnumerable<ITypeSymbol> GetGenericTypeArguments(
        this IFluentReturn fluentReturn,
        IDictionary<FluentType, ITypeSymbol> genericTypeParameterMap)
    {
        return fluentReturn.GenericConstructorParameters
            .SelectMany(parameterSymbol => parameterSymbol.Type.GetGenericTypeArguments())
            .Select(parameter => genericTypeParameterMap.TryGetValue(new FluentType(parameter), out var type)
                ? type
                : parameter)
            .DistinctBy(type => type.ToDisplayString());
    }
}
