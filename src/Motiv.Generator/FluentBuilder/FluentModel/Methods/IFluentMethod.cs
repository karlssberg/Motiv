using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;

namespace Motiv.Generator.FluentBuilder.FluentModel.Methods;

public interface IFluentMethod
{
    string MethodName { get; }
    IParameterSymbol? SourceParameter { get; }
    ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }
    IFluentReturn Return { get; }
    ImmutableArray<FluentTypeParameter> TypeParameters { get; }
    INamespaceSymbol RootNamespace { get; }
    ImmutableArray<FluentMethodParameter> MethodParameters { get; }
}
