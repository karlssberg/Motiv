using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
///     Recursively decomposes a logical expression tree into individual clauses
///     and a composition expression string that describes how to recombine them.
/// </summary>
internal static class ExpressionDecomposer
{
    /// <summary>
    ///     Decomposes a logical expression into clauses and a composition expression.
    /// </summary>
    /// <param name="expression">The root expression to decompose.</param>
    /// <param name="transformClause">A function applied to each leaf clause to transform it (e.g., prefix instance methods).</param>
    /// <returns>An <see cref="ExpressionDecomposition"/> containing the clauses and composition.</returns>
    public static ExpressionDecomposition Decompose(
        ExpressionSyntax expression,
        Func<ExpressionSyntax, ExpressionSyntax> transformClause)
    {
        var counter = 0;
        return DecomposeCore(expression);

        ExpressionDecomposition DecomposeCore(ExpressionSyntax expr) => expr switch
        {
            ParenthesizedExpressionSyntax paren => DecomposeParenthesized(paren),

            PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } unary
                => DecomposeNot(unary),

            BinaryExpressionSyntax binary when GetLogicalOperator(binary) is { } op
                => DecomposeBinary(binary, op),

            _ => CreateLeafClause(expr)
        };

        ExpressionDecomposition DecomposeParenthesized(ParenthesizedExpressionSyntax paren)
        {
            var inner = DecomposeCore(paren.Expression);
            return new ExpressionDecomposition(
                inner.Clauses,
                $"({inner.CompositionExpression})");
        }

        ExpressionDecomposition DecomposeNot(PrefixUnaryExpressionSyntax unary)
        {
            var inner = DecomposeCore(unary.Operand);
            return new ExpressionDecomposition(
                inner.Clauses,
                $"!{inner.CompositionExpression}");
        }

        ExpressionDecomposition DecomposeBinary(BinaryExpressionSyntax binary, (string Op, bool IsInfix) op)
        {
            var left = DecomposeCore(binary.Left);
            var right = DecomposeCore(binary.Right);
            var allClauses = left.Clauses.Concat(right.Clauses).ToList();

            var composition = op.IsInfix
                ? $"{left.CompositionExpression}{op.Op}{right.CompositionExpression}"
                : $"{left.CompositionExpression}{op.Op}({right.CompositionExpression})";

            return new ExpressionDecomposition(allClauses, composition);
        }

        ExpressionDecomposition CreateLeafClause(ExpressionSyntax expr)
        {
            counter++;
            var transformed = transformClause(expr);
            var clauseName = ClauseNameDeriver.DeriveName(expr, counter);
            return new ExpressionDecomposition(
                [(expr.ToString().Trim(), transformed.ToString(), expr)],
                clauseName);
        }
    }

    private static (string Op, bool IsInfix)? GetLogicalOperator(BinaryExpressionSyntax binary) =>
        binary.OperatorToken.Kind() switch
        {
            SyntaxKind.AmpersandAmpersandToken => (".AndAlso", false),
            SyntaxKind.BarBarToken => (".OrElse", false),
            SyntaxKind.CaretToken => (" ^ ", true),
            _ => null
        };
}
