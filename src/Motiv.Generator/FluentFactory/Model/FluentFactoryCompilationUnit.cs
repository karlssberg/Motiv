using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Steps;

namespace Motiv.Generator.FluentFactory.Model;

public record FluentFactoryCompilationUnit(
    INamedTypeSymbol RootType,
    ImmutableArray<IFluentMethod> FluentMethods,
    ImmutableArray<IFluentStep> FluentSteps,
    ImmutableArray<INamespaceSymbol> Usings)
{
    public string Namespace { get; } = RootType.ContainingNamespace.ToDisplayString();

    public ImmutableArray<IFluentMethod> FluentMethods { get; } = FluentMethods;

    public ImmutableArray<IFluentStep> FluentSteps { get; } = FluentSteps;

    public INamedTypeSymbol RootType { get; } = RootType;

    public ImmutableArray<INamespaceSymbol> Usings { get; } = Usings;

    public TypeKind TypeKind { get; set; }

    public Accessibility Accessibility { get; set; }

    public bool IsStatic { get; set; } = true;

    public bool IsRecord { get; set; }

    public IEnumerable<Diagnostic> Diagnostics { get; set; } = [];
}
