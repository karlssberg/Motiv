using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.FluentFactory.Generator.Generation;
using Motiv.FluentFactory.Generator.Model.Steps;
using Motiv.FluentFactory.Generator.Model.Storage;

namespace Motiv.FluentFactory.Generator.Model.Methods;

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

    public string? DocumentationSummary
    {
        get
        {
            var constructorNames = Return.CandidateConstructors
                .Select(ctor => ctor.ToFullDisplayString());

            return Return.CandidateConstructors switch
            {
                { Length: 1 } =>
                    $"""
                     Creates a new instance using constructor {constructorNames.First()}.

                     """,
                { Length: > 1 } =>
                    $"""
                     Creates a new instance using constructors:
                       {string.Join("\n  ", constructorNames)}.

                     """,
                _ => null
            };
        }
    }

    public Dictionary<string, string>? ParameterDocumentation => null; // Creation methods don't use template methods

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
