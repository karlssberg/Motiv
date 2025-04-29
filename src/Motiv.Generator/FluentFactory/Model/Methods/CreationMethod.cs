using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Generation;
using Motiv.Generator.FluentFactory.Model.Steps;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Model.Methods;

public class CreationMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;


    public CreationMethod(
        INamespaceSymbol rootNamespace,
        ConstructorMetadata constructorMetadata,
        ImmutableArray<FluentMethodParameter> availableParameterFields,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueSources)
    {
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetFluentTypeParameter);

        RootNamespace = rootNamespace;
        AvailableParameterFields = availableParameterFields;
        ValueSources = valueSources;
        Return = new TargetTypeReturn(
            constructorMetadata.Constructor,
            [..constructorMetadata.CandidateConstructors],
            new ParameterSequence(availableParameterFields.Select(p => p.ParameterSymbol)));
    }

    public string Name => "Create";

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
