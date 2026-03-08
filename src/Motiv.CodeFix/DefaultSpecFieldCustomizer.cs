using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Default field customizer — uses the proposition type directly with no decoration.
/// </summary>
internal class DefaultSpecFieldCustomizer : ISpecFieldCustomizer
{
    /// <summary>
    ///     Returns the proposition name as an identifier type.
    /// </summary>
    public TypeSyntax GetFieldType(string propositionName, string? modelTypeName) =>
        IdentifierName(propositionName);

    /// <summary>
    ///     Returns a target-typed <c>new()</c> expression.
    /// </summary>
    public ExpressionSyntax GetFieldInitializer(string propositionName) =>
        ImplicitObjectCreationExpression()
            .WithArgumentList(ArgumentList());

    /// <summary>
    ///     Returns <c>new PropositionName(this)</c>.
    /// </summary>
    public ExpressionSyntax GetConstructorAssignment(string propositionName) =>
        ObjectCreationExpression(IdentifierName(propositionName))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(ThisExpression()))));

    /// <summary>
    ///     No additional usings needed.
    /// </summary>
    public IEnumerable<UsingDirectiveSyntax> GetAdditionalUsings() => [];

    /// <summary>
    ///     Returns the member unchanged — no special formatting needed.
    /// </summary>
    public MemberDeclarationSyntax FormatMember(MemberDeclarationSyntax member, SyntaxTrivia lineFeed) =>
        member;
}
