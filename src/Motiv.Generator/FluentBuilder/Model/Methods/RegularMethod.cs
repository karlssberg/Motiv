using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Generation;
using Motiv.Generator.FluentBuilder.Model.Steps;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Model.Methods;

public class RegularMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;

    public RegularMethod(
        string methodName,
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        ImmutableArray<FluentMethodParameter> availableParameterFields,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages)
    {
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);

        MethodName = methodName;
        SourceParameter = sourceParameterSymbol;
        MethodParameters = GetMethodParameters(methodName, sourceParameterSymbol);
        RootNamespace = rootNamespace;
        ValueSources = valueStorages;
        AvailableParameterFields = availableParameterFields;
        Return = fluentReturn;
    }

    public string MethodName { get; }

    public ImmutableArray<FluentMethodParameter> MethodParameters { get; }

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }

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
