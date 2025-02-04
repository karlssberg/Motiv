using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;
using Motiv.Generator.FluentBuilder.Generation;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.FluentModel.Steps;

[DebuggerDisplay("{ToString()}")]
public class RegularFluentStep(INamedTypeSymbol rootType) : IFluentStep
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
            .Where(parameter => parameter.IsOpenGenericType())
    ];

    public Accessibility Accessibility { get; set; } = Accessibility.Internal;

    public override string ToString()
    {
        return string.Join(", ", KnownConstructorParameters.Select(p => p.ToDisplayString()));
    }

    public bool IsEndStep { get; set; }

    public TypeKind TypeKind { get; set; } = TypeKind.Class;

    public bool IsRecord { get; set; }  = false;

    public IReadOnlyDictionary<IParameterSymbol, FluentParameterResolution> ParameterStoreMembers { get; set; } =
        new Dictionary<IParameterSymbol, FluentParameterResolution>(FluentParameterComparer.Default);

    public INamedTypeSymbol RootType { get; } = rootType;

    public string IdentifierDisplayString(INamespaceSymbol _)
    {
        var distinctGenericParameters = GenericConstructorParameters
            .SelectMany(t => t.Type.GetGenericTypeArguments())
            .DistinctBy(symbol => symbol.ToDynamicDisplayString(Namespace))
            .ToArray();

        return distinctGenericParameters.Length > 0
            ? GenericName(Identifier(Name))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList<TypeSyntax>(
                        distinctGenericParameters
                            .Select(arg => IdentifierName(arg.ToDynamicDisplayString(Namespace))))))
                .NormalizeWhitespace()
                .ToString()
            : Name;
    }

    public string IdentifierDisplayString(INamespaceSymbol currentNamespace, IDictionary<FluentType, ITypeSymbol> genericTypeParameterMap)
    {
        var distinctGenericParameters = this.GetGenericTypeArguments(genericTypeParameterMap)
            .ToArray();

        return distinctGenericParameters.Length > 0
            ? GenericName(Identifier(Name))
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList<TypeSyntax>(
                        distinctGenericParameters
                            .Select(arg => IdentifierName(arg.ToDynamicDisplayString(Namespace))))))
                .NormalizeWhitespace()
                .ToString()
            : Name;
    }

    public INamespaceSymbol Namespace => RootType.ContainingNamespace;
    public int Index { get; set; }



    private string GetStepName(INamedTypeSymbol rootType)
    {
        var identifier = rootType.ToIdentifier();
        var name = $"Step_{Index}__{identifier}";

        return name;
    }
}
