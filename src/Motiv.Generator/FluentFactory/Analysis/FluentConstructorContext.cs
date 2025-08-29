using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Model;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Analysis;

[DebuggerDisplay("{ToDisplayString()}}")]
public record FluentConstructorContext
{
    public FluentConstructorContext(
        IMethodSymbol constructor,
        INamedTypeSymbol rootSymbol,
        FluentFactoryMetadata metadata,
        SemanticModel semanticModel)
    {
        Constructor = constructor;
        Options = metadata.Options;
        RootTypeFullName = metadata.RootTypeFullName;
        IsStatic = rootSymbol.IsStatic;
        IsRecord = rootSymbol.IsRecord;
        TypeKind = rootSymbol.TypeKind;
        Accessibility = rootSymbol.DeclaredAccessibility;
        ValueStorage = new ConstructorAnalyzer(semanticModel).FindParameterValueStorage(constructor);
        RootType = rootSymbol;

        // Get all declarations of the type to find modifiers
        var declarations = constructor.ContainingType.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .ToArray();

        // Find declaration with readonly modifier if it exists
        var declaration = declarations.FirstOrDefault(d =>
            d.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)));

        // If no readonly found, use first declaration with any modifiers
        declaration ??= declarations.FirstOrDefault(d => d.Modifiers.Any());

        if (declaration != null)
        {
            OriginalTypeModifiers = declaration.Modifiers;
        }
    }

    public INamedTypeSymbol RootType { get; set; }

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueStorage { get; } =
        new(FluentParameterComparer.Default);

    public FluentFactoryGeneratorOptions Options { get; }

    public bool IsRecord { get; }

    public Accessibility Accessibility { get; }

    public bool IsStatic { get; }

    public TypeKind TypeKind { get; }

    public IMethodSymbol Constructor { get; }

    public string RootTypeFullName { get; }

    public SyntaxTokenList OriginalTypeModifiers { get; }

    public string ToDisplayString() => $"{Constructor.ToDisplayString()}";
}
