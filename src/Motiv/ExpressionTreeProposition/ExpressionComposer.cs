using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal static class ExpressionComposer
{
    internal static Expression<Func<TModel, bool>> Combine<TModel>(
        IExpressionSpec<TModel> left,
        IExpressionSpec<TModel> right,
        Func<Expression, Expression, BinaryExpression> combineOperands)
    {
        var leftExpression = left.ToExpression();
        var rightExpression = right.ToExpression();
        var parameter = leftExpression.Parameters[0];
        var rightBody = ParameterReplacementVisitor.Replace(
            rightExpression.Body,
            rightExpression.Parameters[0],
            parameter);

        return Expression.Lambda<Func<TModel, bool>>(
            combineOperands(leftExpression.Body, rightBody),
            parameter);
    }

    internal static Expression<Func<TModel, bool>> Negate<TModel>(IExpressionSpec<TModel> operand)
    {
        var operandExpression = operand.ToExpression();

        return Expression.Lambda<Func<TModel, bool>>(
            Expression.Not(operandExpression.Body),
            operandExpression.Parameters[0]);
    }
}
