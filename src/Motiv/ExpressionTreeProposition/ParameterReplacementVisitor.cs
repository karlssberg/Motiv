using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ParameterReplacementVisitor(
    ParameterExpression original,
    ParameterExpression replacement) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) =>
        node == original ? replacement : base.VisitParameter(node);

    internal static Expression Replace(
        Expression body,
        ParameterExpression original,
        ParameterExpression replacement) =>
        new ParameterReplacementVisitor(original, replacement).Visit(body);
}
