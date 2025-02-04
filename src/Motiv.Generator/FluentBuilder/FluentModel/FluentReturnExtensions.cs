using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation;

namespace Motiv.Generator.FluentBuilder.FluentModel;

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
