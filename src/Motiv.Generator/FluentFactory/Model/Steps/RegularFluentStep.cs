using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Generation;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Storage;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Model.Steps;

[DebuggerDisplay("{ToString()}")]
public class RegularFluentStep(INamedTypeSymbol rootType, IEnumerable<IMethodSymbol> candidateConstructors) : IFluentStep
{
#if DEBUG
    public int InstanceId => RuntimeHelpers.GetHashCode(this);
#endif
    public string Name => GetStepName(RootType);

    public string FullName => $"{Namespace.ToDisplayString()}.{Name}";

    /// <summary>
    /// The known constructor parameters up until this step.
    /// Potentially more parameters are required to satisfy a constructor signature.
    /// </summary>
    public ParameterSequence KnownConstructorParameters { get; set; } = [];

    public IList<IFluentMethod> FluentMethods { get; set; } = [];

    public ImmutableArray<IParameterSymbol> GenericConstructorParameters => [
        ..KnownConstructorParameters
            .Where(parameter => parameter.Type.IsOpenGenericType())
    ];

    public Accessibility Accessibility { get; set; } = rootType.DeclaredAccessibility;

    public override string ToString()
    {
        return string.Join(", ", KnownConstructorParameters.Select(p => p.ToDisplayString()));
    }

    public bool IsEndStep { get; set; }

    public TypeKind TypeKind { get; set; } = TypeKind.Class;

    public bool IsRecord { get; set; }  = false;

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueStorage { get; set; } = [];

    public INamedTypeSymbol RootType { get; } = rootType;

    public ImmutableArray<IMethodSymbol> CandidateConstructors => [..candidateConstructors];

    public string IdentifierDisplayString(INamespaceSymbol namespaceSymbol)
    {
        var distinctGenericParameters = GenericConstructorParameters
            .SelectMany(t => t.Type.GetGenericTypeArguments())
            .DistinctBy(symbol => symbol.ToDynamicDisplayString(Namespace))
            .ToArray();

        var name = GetMinimalQualifiedName(namespaceSymbol);

        return distinctGenericParameters.Length > 0
            ? GenericName(Identifier(name))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList<TypeSyntax>(
                        distinctGenericParameters
                            .Select(arg => IdentifierName(arg.ToDynamicDisplayString(Namespace))))))
                .NormalizeWhitespace()
                .ToString()
            : name;
    }

    public string IdentifierDisplayString(INamespaceSymbol currentNamespace, IDictionary<FluentType, ITypeSymbol> genericTypeArgumentMap)
    {
        var distinctGenericParameters = this.GetGenericTypeArguments(genericTypeArgumentMap)
            .ToArray();

        var name = GetMinimalQualifiedName(currentNamespace);

        return distinctGenericParameters.Length > 0
            ? GenericName(Identifier(name))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList<TypeSyntax>(
                        distinctGenericParameters
                            .Select(arg => IdentifierName(arg.ToDynamicDisplayString(Namespace))))))
                .NormalizeWhitespace()
                .ToString()
            : name;
    }

    public INamespaceSymbol Namespace => RootType.ContainingNamespace;

    public int Index { get; set; }


    private string GetMinimalQualifiedName(INamespaceSymbol currentNamespace)
    {
        return RootType.ContainingNamespace.Equals(currentNamespace, SymbolEqualityComparer.Default)
            ? Name
            : FullName;
    }

    private string GetStepName(INamedTypeSymbol rootType)
    {
        var identifier = rootType.ToIdentifier();
        var name = $"Step_{Index}__{identifier}";

        return name;
    }
}
