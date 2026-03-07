using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Transforms expression syntax nodes to replace variable references with model member access
///     and prefix unqualified instance method calls.
/// </summary>
internal static class ExpressionTransformer
{
    internal const string InstanceParameterName = "instance";

    /// <summary>
    ///     Prefixes unqualified instance method invocations with <c>instance.</c>.
    /// </summary>
    /// <param name="expression">The expression to transform.</param>
    /// <param name="instanceMethodNames">The set of instance method names to prefix.</param>
    /// <returns>The transformed expression.</returns>
    public static ExpressionSyntax PrefixInstanceMethods(
        ExpressionSyntax expression,
        HashSet<string> instanceMethodNames)
    {
        if (instanceMethodNames.Count == 0) return expression;

        var instanceInvocations = expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is IdentifierNameSyntax id
                          && instanceMethodNames.Contains(id.Identifier.ValueText))
            .ToList();

        if (instanceInvocations.Count == 0) return expression;

        return expression.ReplaceNodes(
            instanceInvocations,
            (original, _) =>
            {
                var methodName = (IdentifierNameSyntax)original.Expression;
                var qualifiedAccess = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(InstanceParameterName),
                    methodName);
                return original.WithExpression(qualifiedAccess);
            });
    }

    /// <summary>
    ///     Converts standalone variable references to model member access (<c>m.PropertyName</c>)
    ///     and prefixes instance method calls.
    /// </summary>
    /// <param name="expression">The expression to transform.</param>
    /// <param name="variableSymbols">The variables to convert.</param>
    /// <param name="instanceMethodNames">Instance method names to prefix.</param>
    /// <returns>The transformed expression.</returns>
    public static ExpressionSyntax ConvertVariablesToModelMemberAccess(
        ExpressionSyntax expression,
        ImmutableArray<ISymbol> variableSymbols,
        HashSet<string> instanceMethodNames)
    {
        var variableNames = variableSymbols.Select(s => s.Name).ToArray();

        // Three-pass approach:
        // 1. Replace member access expressions that start with a variable (e.g., order.Total -> m.Order.Total)
        // 2. Replace standalone identifiers (e.g., x -> m.X)
        // 3. Prefix instance method invocations with "instance." (e.g., IsGreen(x) -> instance.IsGreen(x))

        var result = ReplaceMemberAccessRoots(expression, variableNames);
        result = ReplaceStandaloneIdentifiers(result, variableNames);
        return PrefixInstanceMethods(result, instanceMethodNames);
    }

    private static ExpressionSyntax ReplaceMemberAccessRoots(
        ExpressionSyntax expression,
        string[] variableNames)
    {
        var memberAccessToReplace = expression.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(ma =>
            {
                var expr = ma.Expression;
                while (expr is MemberAccessExpressionSyntax innerMemberAccess)
                    expr = innerMemberAccess.Expression;

                return expr is IdentifierNameSyntax id && variableNames.Contains(id.Identifier.ValueText);
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

                var propertyName = rootId.Identifier.ValueText.Capitalize();
                var newBase = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("m"),
                    IdentifierName(propertyName));

                return RebuildMemberAccessChain(original, newBase);
            });
    }

    private static ExpressionSyntax ReplaceStandaloneIdentifiers(
        ExpressionSyntax expression,
        string[] variableNames)
    {
        var standaloneIdentifiers = expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(id => variableNames.Contains(id.Identifier.ValueText))
            .Where(id => id.Parent is not MemberAccessExpressionSyntax)
            .ToList();

        return expression.ReplaceNodes(
            standaloneIdentifiers,
            (original, _) =>
            {
                var propertyName = original.Identifier.ValueText.Capitalize();
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
        if (original.Expression is IdentifierNameSyntax)
        {
            return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    newBase,
                    original.Name)
                .WithTriviaFrom(original);
        }

        if (original.Expression is MemberAccessExpressionSyntax innerMa)
        {
            var rebuiltInner = RebuildMemberAccessChain(innerMa, newBase);
            return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    rebuiltInner,
                    original.Name)
                .WithTriviaFrom(original);
        }

        return original;
    }
}
