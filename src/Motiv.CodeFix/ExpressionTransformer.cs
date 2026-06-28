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
    ///     Prefixes unqualified static method invocations with the containing class name.
    /// </summary>
    /// <param name="expression">The expression to transform.</param>
    /// <param name="staticMethodNames">The set of static method names to prefix.</param>
    /// <param name="className">The class name to prefix with.</param>
    /// <returns>The transformed expression.</returns>
    public static ExpressionSyntax PrefixStaticMethods(
        ExpressionSyntax expression,
        HashSet<string> staticMethodNames,
        string className)
    {
        if (staticMethodNames.Count == 0) return expression;

        var staticInvocations = expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is IdentifierNameSyntax id
                          && staticMethodNames.Contains(id.Identifier.ValueText))
            .ToList();

        if (staticInvocations.Count == 0) return expression;

        return expression.ReplaceNodes(
            staticInvocations,
            (original, _) =>
            {
                var methodName = (IdentifierNameSyntax)original.Expression;
                var qualifiedAccess = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(className),
                    methodName);
                return original.WithExpression(qualifiedAccess);
            });
    }

    /// <summary>
    ///     Converts standalone variable references to model member access (<c>m.PropertyName</c>)
    ///     and prefixes instance and static method calls.
    /// </summary>
    /// <param name="expression">The expression to transform.</param>
    /// <param name="variableSymbols">The variables to convert.</param>
    /// <param name="instanceMethodNames">Instance method names to prefix.</param>
    /// <param name="staticMethodNames">Static method names to prefix with class name.</param>
    /// <param name="className">The class name for static method qualification.</param>
    /// <returns>The transformed expression.</returns>
    public static ExpressionSyntax ConvertVariablesToModelMemberAccess(
        ExpressionSyntax expression,
        ImmutableArray<ISymbol> variableSymbols,
        HashSet<string> instanceMethodNames,
        HashSet<string>? staticMethodNames = null,
        string? className = null)
    {
        var variableNames = variableSymbols.Select(s => s.Name).ToArray();

        var result = ReplaceMemberAccessRoots(expression, variableNames);
        result = ReplaceStandaloneIdentifiers(result, variableNames);
        result = PrefixInstanceMethods(result, instanceMethodNames);
        if (staticMethodNames != null && className != null)
            result = PrefixStaticMethods(result, staticMethodNames, className);
        return result;
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
