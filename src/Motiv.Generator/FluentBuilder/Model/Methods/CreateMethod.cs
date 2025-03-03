using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Generation;
using Motiv.Generator.FluentBuilder.Model.Steps;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Model.Methods;

public class CreateMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;


    public CreateMethod(
        INamespaceSymbol rootNamespace,
        IMethodSymbol targetConstructor,
        ImmutableArray<FluentMethodParameter> availableParameterFields,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueSources)
    {
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetFluentTypeParameter);

        RootNamespace = rootNamespace;
        AvailableParameterFields = availableParameterFields;
        ValueSources = valueSources;
        Return = new TargetTypeReturn(targetConstructor,
            new ParameterSequence(availableParameterFields.Select(p => p.ParameterSymbol)));
    }

    public string MethodName => "Create";

    public ImmutableArray<FluentMethodParameter> MethodParameters { get; } = [];

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }

    public IParameterSymbol? SourceParameter => null;

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _lazyTypeParameters.Value;
    public INamespaceSymbol RootNamespace { get; }

    private ImmutableArray<FluentTypeParameter> GetFluentTypeParameter()
    {
        return
        [
            ..SourceParameter?.Type
                  .GetGenericTypeParameters()
                  .Select(genericTypeParameter => new FluentTypeParameter(genericTypeParameter))
              ?? []
        ];
    }
}
