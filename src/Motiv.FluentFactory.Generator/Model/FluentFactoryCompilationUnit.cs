using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.FluentFactory.Generator.Model.Methods;
using Motiv.FluentFactory.Generator.Model.Steps;

namespace Motiv.FluentFactory.Generator.Model;

public record FluentFactoryCompilationUnit(INamedTypeSymbol RootType)
{
    public string Namespace { get; set; } = RootType.ContainingNamespace.ToDisplayString();

    public ImmutableArray<IFluentMethod> FluentMethods { get; set; } = [];

    public ImmutableArray<IFluentStep> FluentSteps { get; set; } = [];

    public INamedTypeSymbol RootType { get; set; } = RootType;

    public ImmutableArray<INamespaceSymbol> Usings { get; set; } = [];

    public TypeKind TypeKind { get; set; }

    public Accessibility Accessibility { get; set; }

    public bool IsStatic { get; set; } = true;

    public bool IsRecord { get; set; }

    public IEnumerable<Diagnostic> Diagnostics { get; set; } = [];

    public bool IsEmpty => FluentMethods.IsEmpty && FluentSteps.IsEmpty;
}
