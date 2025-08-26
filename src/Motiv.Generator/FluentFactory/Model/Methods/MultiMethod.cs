using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Generation;
using Motiv.Generator.FluentFactory.Model.Steps;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Model.Methods;

public class MultiMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;

    private readonly Lazy<ImmutableArray<FluentMethodParameter>> _lazyMethodParameters;

    public MultiMethod(
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        IMethodSymbol parameterConverter,
        ImmutableArray<FluentMethodParameter> availableParameterFields,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages,
        ImmutableArray<IMethodSymbol> siblingMultiMethods)
    {
        _lazyMethodParameters = new Lazy<ImmutableArray<FluentMethodParameter>>(GetMethodParameters);
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);
        _lazyResolvedTypes = new Lazy<ImmutableHashSet<ITypeSymbol>>(GetResolvedTypeParameters);

        Name = parameterConverter.Name;
        ValueSources = valueStorages;
        RootNamespace = rootNamespace;
        SourceParameter = sourceParameterSymbol;
        Return = fluentReturn;
        ParameterConverter = parameterConverter;
        AvailableParameterFields = availableParameterFields;
        SiblingMultiMethods = siblingMultiMethods.ToImmutableHashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        DocumentationSummary = sourceParameterSymbol.ContainingSymbol.GetDocumentationCommentXml();
    }

    public ImmutableHashSet<IMethodSymbol> SiblingMultiMethods { get; }

    private readonly Lazy<ImmutableHashSet<ITypeSymbol>> _lazyResolvedTypes;

    public IMethodSymbol ParameterConverter { get; }

    public string Name { get; }

    public ImmutableArray<FluentMethodParameter> MethodParameters => _lazyMethodParameters.Value;

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }
    public string? DocumentationSummary { get; }

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _lazyTypeParameters.Value;

    public INamespaceSymbol RootNamespace { get; }

    public override string ToString() => $"MultiMethod: {ParameterConverter.ToFullDisplayString()}";

    private ImmutableArray<FluentMethodParameter> GetMethodParameters()
    {
        return
        [
            ..ParameterConverter.Parameters
                .Select(p => new FluentMethodParameter(p, Name))
        ];
    }

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        return
        [
            ..ParameterConverter.TypeArguments
                .SelectMany(arg => arg.GetGenericTypeParameters())
                .Except(_lazyResolvedTypes.Value, FluentTypeSymbolEqualityComparer.Default)
                .OfType<ITypeParameterSymbol>()
                .Select(typeParameter => new FluentTypeParameter(typeParameter))
        ];
    }


    private ImmutableHashSet<ITypeSymbol> GetResolvedTypeParameters()
    {
        return ValueSources
            .SelectMany(source => source.Value.Type.GetGenericTypeParameters())
            .ToImmutableHashSet(FluentTypeSymbolEqualityComparer.Default);
    }
}
