using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;

namespace Motiv.Generator.FluentBuilder.FluentModel;

public static class FluentMethodExtensions
{
    // public static IDictionary<FluentMethodParameter, ITypeSymbol> GetParameterTypeMap(this IFluentMethod method)
    // {
    //     var methodParameter = new FluentMethodParameter(method.SourceParameter);
    //     return method.MethodParameters
    //         .ToDictionary(
    //             parameter => parameter,
    //             parameter => parameter.Type);
    // }
}
