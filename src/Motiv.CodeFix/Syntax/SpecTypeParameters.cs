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

    /// <summary>An expression that depends on no type parameters.</summary>
    public static readonly SpecTypeParameters None = new([]);

    private readonly ImmutableArray<ITypeParameterSymbol> _typeParameters;
    private readonly SemanticModel? _semanticModel;
    private readonly int _specPosition;

    private SpecTypeParameters(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        SemanticModel? semanticModel = null,
        int specPosition = 0)
    {
        _typeParameters = typeParameters;
        _semanticModel = semanticModel;
        _specPosition = specPosition;
    }

    public bool IsEmpty => _typeParameters.IsEmpty;

    /// <summary>Whether a type parameter is the enclosing method's, which no field of the containing type can name.</summary>
    public bool DeclaresMethodTypeParameters =>
        _typeParameters.Any(typeParameter => typeParameter.TypeParameterKind == TypeParameterKind.Method);

    /// <summary>Whether any type, method or local function around <paramref name="node" /> declares type parameters.</summary>
    public static bool AnyInScope(SyntaxNode node) =>
        node.Ancestors().Any(ancestor => ancestor switch
        {
            TypeDeclarationSyntax type => type.TypeParameterList is not null,
            MethodDeclarationSyntax method => method.TypeParameterList is not null,
            LocalFunctionStatementSyntax localFunction => localFunction.TypeParameterList is not null,
            _ => false
        });

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

        // A constraint may name another type parameter (where TList : ICollection<TItem>), which must be declared too
        var found = new List<ITypeParameterSymbol>();
        var pending = new Queue<ITypeParameterSymbol>(valueTypes.SelectMany(TypeParametersIn).Concat(namedInExpression));
        while (pending.Count > 0)
        {
            var typeParameter = pending.Dequeue();
            if (found.Contains(typeParameter, SymbolEqualityComparer.Default))
                continue;

            found.Add(typeParameter);
            foreach (var referenced in typeParameter.ConstraintTypes.SelectMany(TypeParametersIn))
                pending.Enqueue(referenced);
        }

        // The spec is declared beside the outermost type, so its constraints name types as the source would there
        var specPosition = expression.Ancestors().OfType<BaseTypeDeclarationSyntax>().Last().SpanStart;
        return new SpecTypeParameters(
        [
            ..found
                .OrderBy(typeParameter => typeParameter.TypeParameterKind)
                .ThenBy(typeParameter => typeParameter.Ordinal)
        ], semanticModel, specPosition);
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
            // A nested type of a generic type depends on the outer type's parameters too: Tree<T>.Node
            INamedTypeSymbol named => named.TypeArguments.SelectMany(TypeParametersIn)
                .Concat(named.ContainingType is { } outer ? TypeParametersIn(outer) : []),
            IArrayTypeSymbol array => TypeParametersIn(array.ElementType),
            _ => []
        };

    /// <summary>
    ///     The constraint clause for <paramref name="typeParameter" />, built from the symbol rather than copied from
    ///     syntax: an override inherits constraints it may not restate, and each part of a partial type may repeat them.
    /// </summary>
    private IEnumerable<TypeParameterConstraintClauseSyntax> DeclaredConstraints(ITypeParameterSymbol typeParameter)
    {
        var constraints = new List<TypeParameterConstraintSyntax>();
        if (typeParameter.HasReferenceTypeConstraint)
            constraints.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                ? ClassOrStructConstraint(SyntaxKind.ClassConstraint).WithQuestionToken(Token(SyntaxKind.QuestionToken))
                : ClassOrStructConstraint(SyntaxKind.ClassConstraint));
        else if (typeParameter.HasUnmanagedTypeConstraint)
            constraints.Add(TypeConstraint(IdentifierName("unmanaged")));
        else if (typeParameter.HasValueTypeConstraint)
            constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));
        else if (typeParameter.HasNotNullConstraint)
            constraints.Add(TypeConstraint(IdentifierName("notnull")));

        constraints.AddRange(typeParameter.ConstraintTypes.Select(type =>
            TypeConstraint(ParseTypeName(type.ToMinimalDisplayString(_semanticModel!, _specPosition)))));

        if (typeParameter.HasConstructorConstraint)
            constraints.Add(ConstructorConstraint());

        return constraints.Count == 0
            ? []
            : [TypeParameterConstraintClause(IdentifierName(typeParameter.Name)).WithConstraints(SeparatedList(constraints))];
    }
}
