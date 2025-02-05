using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation;

namespace Motiv.Generator.FluentBuilder.FluentModel.Methods;

public class RegularMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;

    public RegularMethod(
        string methodName,
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        ImmutableArray<FluentMethodParameter> availableParameterFields)
    {
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);

        MethodName = methodName;
        SourceParameter = sourceParameterSymbol;
        MethodParameters = GetMethodParameters(methodName, sourceParameterSymbol);
        RootNamespace = rootNamespace;
        AvailableParameterFields = availableParameterFields;
        Return = fluentReturn;
    }

    public string MethodName { get; }

    public ImmutableArray<FluentMethodParameter> MethodParameters { get; }

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _lazyTypeParameters.Value;

    public INamespaceSymbol RootNamespace { get; }

    private static ImmutableArray<FluentMethodParameter> GetMethodParameters(string methodName,
        IParameterSymbol sourceParameterSymbol)
    {
        return [new FluentMethodParameter(sourceParameterSymbol, methodName)];
    }

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        return
        [
            ..SourceParameter.Type
                .GetGenericTypeParameters()
                .Select(genericTypeParameter => new FluentTypeParameter(genericTypeParameter))
        ];
    }
}
