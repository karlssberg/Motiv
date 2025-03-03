using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Model.Steps;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Model.Methods;

public class MultiMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;

    private readonly Lazy<ImmutableArray<FluentMethodParameter>> _lazyMethodParameters;

    public MultiMethod(IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        IMethodSymbol parameterConverter,
        ImmutableArray<FluentMethodParameter> availableParameterFields,
        OrderedDictionary<IParameterSymbol,
        IFluentValueStorage> valueStorages)
    {
        _lazyMethodParameters = new Lazy<ImmutableArray<FluentMethodParameter>>(GetMethodParameters);
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);

        MethodName = parameterConverter.Name;
        ValueSources = valueStorages;
        RootNamespace = rootNamespace;
        SourceParameter = sourceParameterSymbol;
        Return = fluentReturn;
        ParameterConverter = parameterConverter;
        AvailableParameterFields = availableParameterFields;
    }

    public IMethodSymbol ParameterConverter { get; }

    public string MethodName { get; }

    public ImmutableArray<FluentMethodParameter> MethodParameters => _lazyMethodParameters.Value;

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _lazyTypeParameters.Value;

    public INamespaceSymbol RootNamespace { get; }

    private ImmutableArray<FluentMethodParameter> GetMethodParameters()
    {
        return [..ParameterConverter.Parameters.Select(p => new FluentMethodParameter(p, MethodName))];
    }

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        return
        [
            ..ParameterConverter.TypeArguments
                .OfType<ITypeParameterSymbol>()
                .Select(typeParameter => new FluentTypeParameter(typeParameter))
        ];
    }
}
