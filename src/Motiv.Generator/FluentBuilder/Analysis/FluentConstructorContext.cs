using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Model;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Analysis;

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

    public string ToDisplayString() => $"{Constructor.ToDisplayString()}";
}
