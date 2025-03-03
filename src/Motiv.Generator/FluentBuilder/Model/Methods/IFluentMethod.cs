using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Model.Steps;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Model.Methods;

public interface IFluentMethod
{
    string MethodName { get; }
    IParameterSymbol? SourceParameter { get; }
    ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }
    IFluentReturn Return { get; }
    ImmutableArray<FluentTypeParameter> TypeParameters { get; }
    INamespaceSymbol RootNamespace { get; }
    ImmutableArray<FluentMethodParameter> MethodParameters { get; }
    OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }
}
