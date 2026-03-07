using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Restructures <c>&amp;&amp;</c> and <c>||</c> chains so that clauses containing instance
///     method invocations are grouped together in parentheses, enabling correct decomposition.
/// </summary>
public static class LogicalChainGrouper
{
    /// <summary>
    ///     Recursively groups instance-method-containing clauses within an expression.
    /// </summary>
    /// <param name="expression">The expression to restructure.</param>
    /// <returns>The restructured expression with instance method clauses grouped.</returns>
    public static ExpressionSyntax Group(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedExpressionSyntax paren =>
                ParenthesizedExpression(Group(paren.Expression)),

            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) =>
                TryRegroupChain(binary, SyntaxKind.LogicalAndExpression),

            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalOrExpression) =>
                TryRegroupChain(binary, SyntaxKind.LogicalOrExpression),

            _ => expression
        };
    }

    private static ExpressionSyntax TryRegroupChain(
        BinaryExpressionSyntax chain,
        SyntaxKind operatorKind)
    {
        var leaves = FlattenChain(chain, operatorKind)
            .Select(Group)
            .ToList();

        var splitIndex = leaves.FindIndex(ContainsInvocation);
        if (splitIndex <= 0 || leaves.Count - splitIndex < 2)
            return BuildChain(leaves, operatorKind);

        var left = BuildChain(leaves.GetRange(0, splitIndex), operatorKind);
        var right = ParenthesizedExpression(
            BuildChain(leaves.GetRange(splitIndex, leaves.Count - splitIndex), operatorKind));
        return BinaryExpression(operatorKind, left, right);
    }

    private static bool ContainsInvocation(ExpressionSyntax expr) =>
        expr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any();

    private static List<ExpressionSyntax> FlattenChain(
        BinaryExpressionSyntax binary,
        SyntaxKind operatorKind)
    {
        var leaves = new List<ExpressionSyntax>();
        FlattenChainCore(binary, operatorKind, leaves);
        return leaves;
    }

    private static void FlattenChainCore(
        BinaryExpressionSyntax binary,
        SyntaxKind operatorKind,
        List<ExpressionSyntax> leaves)
    {
        while (true)
        {
            if (binary.Left is BinaryExpressionSyntax left && left.IsKind(operatorKind))
                FlattenChainCore(left, operatorKind, leaves);
            else
                leaves.Add(binary.Left);

            if (binary.Right is BinaryExpressionSyntax right && right.IsKind(operatorKind))
            {
                binary = right;
                continue;
            }

            leaves.Add(binary.Right);
            break;
        }
    }

    private static ExpressionSyntax BuildChain(List<ExpressionSyntax> leaves, SyntaxKind operatorKind)
    {
        var result = leaves[0];
        for (var i = 1; i < leaves.Count; i++)
            result = BinaryExpression(operatorKind, result, leaves[i]);
        return result;
    }
}
