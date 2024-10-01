using System.Linq.Expressions;

namespace Motiv.ExpressionTrees;

internal class ExpressionTreeTransformer<TModel>(
    IExpressionSerializer expressionSerializer,
    ExpressionTreeTransformerOptions? options = null)
{
    private readonly ExpressionTreeTransformerOptions _options = options ?? new ExpressionTreeTransformerOptions();

    internal SpecBase<TModel, string> Transform(Expression<Func<TModel, bool>> expression) =>
        Transform(expression.Body, expression.Parameters.First());

    private SpecBase<TModel, string> Transform(
        Expression expression,
        ParameterExpression parameter)
    {
        return expression switch
        {
            BinaryExpression { NodeType: ExpressionType.And } binaryExpression =>
                Left(binaryExpression).And(Right(binaryExpression)),
            BinaryExpression { NodeType: ExpressionType.AndAlso } binaryExpression =>
                Left(binaryExpression).AndAlso(Right(binaryExpression)),
            BinaryExpression { NodeType: ExpressionType.Or } binaryExpression =>
                Left(binaryExpression).Or(Right(binaryExpression)),
            BinaryExpression { NodeType: ExpressionType.OrElse } binaryExpression =>
                Left(binaryExpression).OrElse(Right(binaryExpression)),
            BinaryExpression { NodeType: ExpressionType.ExclusiveOr } binaryExpression =>
                Left(binaryExpression).XOr(Right(binaryExpression)),
            BinaryExpression { NodeType: ExpressionType.GreaterThan } binaryExpression =>
                TransformGreaterThanExpression(binaryExpression, parameter),
            BinaryExpression { NodeType: ExpressionType.GreaterThanOrEqual } binaryExpression =>
                TransformGreaterThanOrEqualExpression(binaryExpression, parameter),
            BinaryExpression { NodeType: ExpressionType.LessThan } binaryExpression =>
                TransformLessThanExpression(binaryExpression, parameter),
            BinaryExpression { NodeType: ExpressionType.LessThanOrEqual } binaryExpression =>
                TransformLessThanOrEqualExpression(binaryExpression, parameter),
            BinaryExpression { NodeType: ExpressionType.Equal } binaryExpression =>
                TransformEqualsExpression(binaryExpression, parameter),
            BinaryExpression { NodeType: ExpressionType.NotEqual } binaryExpression =>
                TransformNotEqualsExpression(binaryExpression, parameter),
            UnaryExpression { NodeType: ExpressionType.Not } unaryExpression =>
                TransformUnaryExpression(unaryExpression, parameter),
            ConditionalExpression conditionalExpression when conditionalExpression.Type == typeof(bool) =>
                TransformBooleanConditionalExpression(conditionalExpression, parameter),
            _ =>
                TransformQuasiProposition(expression, parameter)
        };

        SpecBase<TModel, string> Right(BinaryExpression binaryExpression) =>
            Transform(binaryExpression.Right, parameter);

        SpecBase<TModel, string> Left(BinaryExpression binaryExpression) => Transform(binaryExpression.Left, parameter);
    }

    private SpecBase<TModel, string> TransformBooleanConditionalExpression(
        ConditionalExpression expression,
        ParameterExpression parameter)
    {
        var test = Transform(expression.Test, parameter);
        var ifTrue = Transform(expression.IfTrue, parameter);
        var ifFalse = Transform(expression.IfFalse, parameter);

        var whenConditional = Humanize(expression);

        return
            Spec.Build((TModel model) =>
                {
                    var antecedent = test.IsSatisfiedBy(model);
                    if (antecedent) return antecedent & ifTrue.IsSatisfiedBy(model);

                    var consequent = ifFalse.IsSatisfiedBy(model);
                    return (antecedent & consequent) | (antecedent & !consequent);
                })
                .WhenTrueYield((_, result) => result.Assertions)
                .WhenFalseYield((_, result) => result.Assertions)
            .Create(whenConditional);
    }

    private SpecBase<TModel, string> TransformEqualsExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var trueBecause = Humanize(Expression.Equal(expression.Left, expression.Right));
        var falseBecause = Humanize(Expression.NotEqual(expression.Left, expression.Right));

        return
            Spec.Build(predicate)
                .WhenTrue(trueBecause)
                .WhenFalse(falseBecause)
                .Create();
    }

    private SpecBase<TModel, string> TransformNotEqualsExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var trueBecause = Humanize(Expression.NotEqual(expression.Left, expression.Right));
        var falseBecause = Humanize(Expression.Equal(expression.Left, expression.Right));

        return
            Spec.Build(predicate)
                .WhenTrue(trueBecause)
                .WhenFalse(falseBecause)
                .Create();
    }

    private SpecBase<TModel, string> TransformGreaterThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var whenGreaterThan = Humanize(Expression.GreaterThan(expression.Left, expression.Right));
        var whenEqual = Humanize(Expression.Equal(expression.Left, expression.Right));
        var whenLessThan = Humanize(Expression.LessThan(expression.Left, expression.Right));

        return
            Spec.Build(predicate)
                .WhenTrue(whenGreaterThan)
                .WhenFalse(model => equals(model) ? whenEqual : whenLessThan)
                .Create();
    }

    private SpecBase<TModel, string> TransformGreaterThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var whenGreaterThanOrEqual = Humanize(Expression.GreaterThanOrEqual(expression.Left, expression.Right));
        var whenGreaterThan = Humanize(Expression.GreaterThan(expression.Left, expression.Right));
        var whenEqual = Humanize(Expression.Equal(expression.Left, expression.Right));
        var whenLessThan = Humanize(Expression.LessThan(expression.Left, expression.Right));

        return
            Spec.Build(predicate)
                .WhenTrue(model => equals(model) ? whenEqual : whenGreaterThan)
                .WhenFalse(whenLessThan)
                .Create(whenGreaterThanOrEqual);
    }

    private SpecBase<TModel, string> TransformLessThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var whenGreaterThan = Humanize(Expression.GreaterThan(expression.Left, expression.Right));
        var whenEqual = Humanize(Expression.Equal(expression.Left, expression.Right));
        var whenLessThan = Humanize(Expression.LessThan(expression.Left, expression.Right));

        return
            Spec.Build(predicate)
                .WhenTrue(whenLessThan)
                .WhenFalse(model => equals(model) ? whenEqual : whenGreaterThan)
                .Create();
    }

    private SpecBase<TModel, string> TransformLessThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var whenLessThanOrEqual = Humanize(Expression.LessThanOrEqual(expression.Left, expression.Right));
        var whenGreaterThan = Humanize(Expression.GreaterThan(expression.Left, expression.Right));
        var whenEqual = Humanize(Expression.Equal(expression.Left, expression.Right));
        var whenLessThan = Humanize(Expression.LessThan(expression.Left, expression.Right));

        return
            Spec.Build(predicate)
                .WhenTrue(model => equals(model) ? whenEqual : whenLessThan)
                .WhenFalse(whenGreaterThan)
                .Create(whenLessThanOrEqual);
    }

    private SpecBase<TModel, string> TransformUnaryExpression(
        UnaryExpression expression,
        ParameterExpression parameter)
    {
        var operand = Transform(expression.Operand, parameter);
        return expression.NodeType switch
        {
            ExpressionType.Not => operand.Not(),
            _ => TransformQuasiProposition(expression, parameter)
        };
    }

    private SpecBase<TModel, string> TransformQuasiProposition(
        Expression expression,
        ParameterExpression parameter)
    {
        var trueBecause = Humanize(Expression.Equal(expression, Expression.Constant(true)));
        var falseBecause = Humanize(Expression.Equal(expression, Expression.Constant(false)));

        return
            Spec.Build(CreatePredicate(expression, parameter))
                .WhenTrue(trueBecause)
                .WhenFalse(falseBecause)
                .Create();
    }

    private Func<TModel, bool> CreatePredicate(
        Expression expression,
        ParameterExpression parameter) =>
        CreateFunc<TModel, bool>(expression, parameter);

    private Func<TModel, bool> CreateEqualsFunction(
        BinaryExpression expression,
        ParameterExpression parameter) =>
        CreateFunc<TModel, bool>(Expression.Equal(expression.Left, expression.Right), parameter);

    private Func<TValue, TReturn> CreateFunc<TValue, TReturn>(
        Expression expression,
        ParameterExpression parameter) =>
        Expression.Lambda<Func<TValue, TReturn>>(expression, parameter).Compile();

    private string Humanize(Expression expression) =>
        expressionSerializer.Serialize(expression);
}
