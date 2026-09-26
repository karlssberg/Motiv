using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     The type parameters of the surrounding method or type that a converted expression depends on. A spec
///     class at namespace level cannot see them, so it declares them itself, with their constraints, and —
///     since a field on the containing type cannot be of a method's type parameter — holds its own instance
///     in a static field, which the runtime keeps once per closed type.
/// </summary>
public sealed class SpecTypeParameters
{
    public const string InstanceFieldName = "Instance";

    private readonly ImmutableArray<ITypeParameterSymbol> _typeParameters;

    private SpecTypeParameters(ImmutableArray<ITypeParameterSymbol> typeParameters) =>
        _typeParameters = typeParameters;

    public bool IsEmpty => _typeParameters.IsEmpty;

    /// <summary>
    ///     Finds the type parameters named in <paramref name="expression" /> or in the types of the values it reads,
    ///     the containing type's before the method's, each in declaration order.
    /// </summary>
    public static SpecTypeParameters Find(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IEnumerable<ITypeSymbol?> valueTypes)
    {
        var namedInExpression = expression
            .DescendantNodesAndSelf()
            .OfType<TypeSyntax>()
            .Select(type => semanticModel.GetSymbolInfo(type).Symbol)
            .OfType<ITypeParameterSymbol>();

        return new SpecTypeParameters(
        [
            ..valueTypes
                .SelectMany(TypeParametersIn)
                .Concat(namedInExpression)
                .Distinct<ITypeParameterSymbol>(SymbolEqualityComparer.Default)
                .OrderBy(typeParameter => typeParameter.TypeParameterKind)
                .ThenBy(typeParameter => typeParameter.Ordinal)
        ]);
    }

    /// <summary>
    ///     <paramref name="propositionName" /> with its type arguments, e.g. <c>XProposition&lt;T&gt;</c>.
    /// </summary>
    public string Qualify(string propositionName) =>
        IsEmpty
            ? propositionName
            : $"{propositionName}<{string.Join(", ", _typeParameters.Select(typeParameter => typeParameter.Name))}>";

    /// <summary>
    ///     Declares the type parameters and their constraints on <paramref name="classDeclaration" />.
    /// </summary>
    public ClassDeclarationSyntax ApplyTo(ClassDeclarationSyntax classDeclaration) =>
        IsEmpty
            ? classDeclaration
            : classDeclaration
                .WithTypeParameterList(TypeParameterList(SeparatedList(
                    _typeParameters.Select(typeParameter => TypeParameter(typeParameter.Name)))))
                .WithConstraintClauses(List(_typeParameters.SelectMany(DeclaredConstraints)));

    private static IEnumerable<ITypeParameterSymbol> TypeParametersIn(ITypeSymbol? type) =>
        type switch
        {
            ITypeParameterSymbol typeParameter => [typeParameter],
            INamedTypeSymbol named => named.TypeArguments.SelectMany(TypeParametersIn),
            IArrayTypeSymbol array => TypeParametersIn(array.ElementType),
            _ => []
        };

    private static IEnumerable<TypeParameterConstraintClauseSyntax> DeclaredConstraints(ITypeParameterSymbol typeParameter) =>
        typeParameter.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .Select(syntax => syntax.Parent?.Parent switch
            {
                MethodDeclarationSyntax method => method.ConstraintClauses,
                TypeDeclarationSyntax type => type.ConstraintClauses,
                LocalFunctionStatementSyntax localFunction => localFunction.ConstraintClauses,
                _ => default
            })
            .SelectMany(clauses => clauses)
            .Where(clause => clause.Name.Identifier.ValueText == typeParameter.Name)
            .Select(clause => clause.WithoutTrivia());
}
