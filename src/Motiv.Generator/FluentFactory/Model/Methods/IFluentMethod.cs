using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model.Steps;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Model.Methods;

public interface IFluentMethod
{
    string Name { get; }
    IParameterSymbol? SourceParameter { get; }
    ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }
    IFluentReturn Return { get; }
    ImmutableArray<FluentTypeParameter> TypeParameters { get; }
    INamespaceSymbol RootNamespace { get; }
    ImmutableArray<FluentMethodParameter> MethodParameters { get; }
    OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }
    string? DocumentationSummary { get; }
}
