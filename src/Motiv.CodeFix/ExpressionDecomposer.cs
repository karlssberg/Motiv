using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.CodeFix.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Recursively decomposes a logical expression tree into individual clauses
///     and a composition expression tree that describes how to recombine them.
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

        // The source's parentheses shaped the syntax tree; the composition parenthesizes by its own precedence
        ExpressionDecomposition DecomposeParenthesized(ParenthesizedExpressionSyntax paren) =>
            DecomposeCore(paren.Expression);

        ExpressionDecomposition DecomposeNot(PrefixUnaryExpressionSyntax unary)
        {
            var inner = DecomposeCore(unary.Operand);
            var operand = inner.CompositionExpression is IdentifierNameSyntax
                ? inner.CompositionExpression
                : ParenthesizedExpression(inner.CompositionExpression);
            return new ExpressionDecomposition(
                inner.Clauses,
                PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand));
        }

        ExpressionDecomposition DecomposeBinary(BinaryExpressionSyntax binary, (string Op, bool IsInfix) op)
        {
            var left = DecomposeCore(binary.Left);
            var right = DecomposeCore(binary.Right);
            var allClauses = left.Clauses.Concat(right.Clauses).ToList();

            ExpressionSyntax composition;
            if (op.IsInfix)
            {
                // `^` is left-associative, so a right operand that is itself a `^` needs parentheses to keep its grouping
                composition = BinaryExpression(
                    SyntaxKind.ExclusiveOrExpression,
                    left.CompositionExpression,
                    right.CompositionExpression is BinaryExpressionSyntax
                        ? ParenthesizedExpression(right.CompositionExpression)
                        : right.CompositionExpression);
            }
            else
            {
                // Member access binds tighter than `!` and `^`, so a receiver that is either needs parentheses
                var receiver = left.CompositionExpression is PrefixUnaryExpressionSyntax or BinaryExpressionSyntax
                    ? ParenthesizedExpression(left.CompositionExpression)
                    : left.CompositionExpression;
                var methodName = op.Op == ".AndAlso" ? "AndAlso" : "OrElse";
                composition = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        receiver,
                        IdentifierName(methodName)),
                    ArgumentList(SingletonSeparatedList(Argument(right.CompositionExpression))));
            }

            return new ExpressionDecomposition(allClauses, composition);
        }

        ExpressionDecomposition CreateLeafClause(ExpressionSyntax expr)
        {
            counter++;
            var transformed = transformClause(expr);
            // A positional placeholder: two different clauses can derive the same name, so ClauseSet names them
            return new ExpressionDecomposition(
                [(expr.ToString().Trim(), transformed, expr)],
                IdentifierName(ClauseSet.Placeholder(counter)));
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
