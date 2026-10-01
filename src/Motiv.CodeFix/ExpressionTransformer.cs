using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Transforms expression syntax nodes to replace variable references with model member access.
/// </summary>
internal static class ExpressionTransformer
{
    /// <summary>
    ///     Converts standalone variable references to model member access (<c>m.PropertyName</c>).
    /// </summary>
    /// <param name="expression">The expression to transform.</param>
    /// <param name="memberNames">Each variable's name mapped to its model member, from <see cref="ModelMemberNames" />.</param>
    /// <returns>The transformed expression.</returns>
    public static ExpressionSyntax ConvertVariablesToModelMemberAccess(
        ExpressionSyntax expression,
        IReadOnlyDictionary<string, string> memberNames) =>
        ReplaceStandaloneIdentifiers(ReplaceMemberAccessRoots(expression, memberNames), memberNames);

    /// <summary>
    ///     The model member each variable is read through: its name Pascal-cased without a field's leading
    ///     underscores, unless that would give two variables one member, as <c>limit</c> and <c>_limit</c> would; and
    ///     as written when even capitalizing would, as <c>limit</c> and <c>Limit</c> would.
    /// </summary>
    /// <param name="variableSymbols">The variables the model holds.</param>
    /// <returns>Each variable's name mapped to its model member's name.</returns>
    public static IReadOnlyDictionary<string, string> ModelMemberNames(IEnumerable<ISymbol> variableSymbols) =>
        variableSymbols
            .Select(symbol => symbol.Name)
            .Distinct()
            .GroupBy(name => name.ToPascalCase())
            .SelectMany(group => group.Select(name =>
                (Name: name, Member: group.Count() == 1 ? group.Key : DistinctMemberName(name, group))))
            .ToDictionary(pair => pair.Name, pair => pair.Member);

    // Capitalized, unless another name capitalizes the same way (limit and Limit), when it stays as written
    private static string DistinctMemberName(string name, IEnumerable<string> namesSharingMember) =>
        namesSharingMember.Count(other => other.Capitalize() == name.Capitalize()) == 1 ? name.Capitalize() : name;

    private static ExpressionSyntax ReplaceMemberAccessRoots(
        ExpressionSyntax expression,
        IReadOnlyDictionary<string, string> memberNames)
    {
        var memberAccessToReplace = expression.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(ma =>
            {
                var expr = ma.Expression;
                while (expr is MemberAccessExpressionSyntax innerMemberAccess)
                    expr = innerMemberAccess.Expression;

                return expr is IdentifierNameSyntax id && memberNames.ContainsKey(id.Identifier.ValueText);
            })
            .ToList();

        return expression.ReplaceNodes(
            memberAccessToReplace,
            (original, _) =>
            {
                var expr = original.Expression;
                while (expr is MemberAccessExpressionSyntax innerMa)
                    expr = innerMa.Expression;

                if (expr is not IdentifierNameSyntax rootId)
                    return original;

                var propertyName = memberNames[rootId.Identifier.ValueText];
                var newBase = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("m"),
                    IdentifierName(propertyName));

                return RebuildMemberAccessChain(original, newBase);
            });
    }

    private static ExpressionSyntax ReplaceStandaloneIdentifiers(
        ExpressionSyntax expression,
        IReadOnlyDictionary<string, string> memberNames)
    {
        var standaloneIdentifiers = expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(id => memberNames.ContainsKey(id.Identifier.ValueText))
            .Where(id => id.Parent is not MemberAccessExpressionSyntax)
            .ToList();

        return expression.ReplaceNodes(
            standaloneIdentifiers,
            (original, _) =>
            {
                var propertyName = memberNames[original.Identifier.ValueText];
                return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(propertyName))
                    .WithTriviaFrom(original);
            });
    }

    private static ExpressionSyntax RebuildMemberAccessChain(
        MemberAccessExpressionSyntax original,
        ExpressionSyntax newBase)
    {
        return original.Expression switch
        {
            IdentifierNameSyntax =>
                MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        newBase,
                        original.Name)
                    .WithTriviaFrom(original),

            MemberAccessExpressionSyntax innerMa =>
                MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                    RebuildMemberAccessChain(innerMa, newBase),
                        original.Name)
                    .WithTriviaFrom(original),

            _ => original
        };
    }
}
