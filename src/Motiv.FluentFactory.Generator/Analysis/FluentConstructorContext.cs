using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Model;
using Motiv.FluentFactory.Generator.Model.Storage;

namespace Motiv.FluentFactory.Generator.Analysis;

[DebuggerDisplay("{ToDisplayString()}}")]
public record FluentConstructorContext
{
    public FluentConstructorContext(
        IMethodSymbol constructor,
        AttributeData attributeData,
        INamedTypeSymbol rootSymbol,
        FluentFactoryMetadata metadata,
        bool isAttributedUsedOnContainingType,
        SemanticModel semanticModel)
    {
        Constructor = constructor;
        AttributeData = attributeData;
        Options = metadata.Options;
        RootTypeFullName = metadata.RootTypeFullName;
        CreateMethodName = metadata.CreateMethodName;
        IsAttributedUsedOnContainingType = isAttributedUsedOnContainingType;
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
    public AttributeData AttributeData { get; }

    public string RootTypeFullName { get; }

    public string? CreateMethodName { get; }

    public bool IsAttributedUsedOnContainingType { get; }

    public SyntaxTokenList OriginalTypeModifiers { get; }

    public string ToDisplayString() => $"{Constructor.ToDisplayString()}";
}
