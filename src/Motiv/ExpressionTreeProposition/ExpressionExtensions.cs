using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal static class ExpressionExtensions
{
    internal static Expression ToAssertionExpression(this Expression expression, bool satisfied)
    {
        return (expression, satisfied) switch
        {
            (ConstantExpression constantExpression, _) when constantExpression.Type == typeof(bool) =>
                constantExpression,
            (TypeBinaryExpression { NodeType: ExpressionType.TypeIs } typeIsExpression, true) =>
                typeIsExpression,
            (TypeBinaryExpression { NodeType: ExpressionType.TypeIs } typeIsExpression, false) =>
                Expression.Not(typeIsExpression),
            _ =>
                Expression.Equal(expression, Expression.Constant(satisfied)),
        };
    }

    internal static string ToAssertion(this LambdaExpression expression, bool satisfied)
    {
        var body = GetAssertionExpression(expression.Body, satisfied);

        return Expression
            .Lambda(body, expression.Parameters)
            .Serialize();
    }

    internal static string ToAssertion(this Expression expression, bool satisfied)
    {
        return GetAssertionExpression(expression, satisfied).Serialize();
    }

    private static Expression GetAssertionExpression(this Expression expression, bool satisfied)
    {
        return Expression.Equal(
            Expression.Convert(expression, typeof(object)),
            Expression.Convert(Expression.Constant(satisfied), typeof(object)));
    }
}
