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

        var expressionTest = Humanize(expression.Test);
        var expressionIfTrue = Humanize(expression.IfTrue);
        var expressionIfFalse = Humanize(expression.IfFalse);

        return Spec.Build((TModel model) =>
            {
                var antecedent = test.IsSatisfiedBy(model);
                if (antecedent) return antecedent & ifTrue.IsSatisfiedBy(model);

                var consequent = ifFalse.IsSatisfiedBy(model);
                return (antecedent & consequent) | (antecedent & !consequent);
            })
            .WhenTrueYield((_, result) => result.Assertions)
            .WhenFalseYield((_, result) => result.Assertions)
            .Create($"{expressionTest} ? {expressionIfTrue} : {expressionIfFalse}");
    }

    private SpecBase<TModel, string> TransformEqualsExpression(
        BinaryExpression expression,
        ParameterExpression parameter) =>
        CreateEqualitySpec(
            expression,
            parameter,
            _options.EqualsToken,
            _options.NotEqualsToken);

    private SpecBase<TModel, string> TransformNotEqualsExpression(
        BinaryExpression expression,
        ParameterExpression parameter) =>
        CreateEqualitySpec(
            expression,
            parameter,
            _options.NotEqualsToken,
            _options.EqualsToken);

    private SpecBase<TModel, string> TransformGreaterThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);
        var expressionLeft = Humanize(expression.Left);
        var expressionRight = Humanize(expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue($"{expressionLeft} > {expressionRight}")
                .WhenFalse(model => equals(model)
                    ? $"{expressionLeft} {_options.EqualsToken} {expressionRight}"
                    : $"{expressionLeft} {_options.LessThanToken} {expressionRight}")
                .Create();
    }

    private SpecBase<TModel, string> TransformGreaterThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);
        var expressionLeft = Humanize(expression.Left);
        var expressionRight = Humanize(expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => equals(model)
                    ? $"{expressionLeft} {_options.EqualsToken} {expressionRight}"
                    : $"{expressionLeft} {_options.GreaterThanToken} {expressionRight}")
                .WhenFalse($"{expressionLeft} {_options.LessThanToken} {expressionRight}")
                .Create($"{expressionLeft} {_options.GreaterThanOrEqualToken} {expressionRight}");
    }

    private SpecBase<TModel, string> TransformLessThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);
        var expressionLeft = Humanize(expression.Left);
        var expressionRight = Humanize(expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue($"{expressionLeft} {_options.LessThanToken} {expressionRight}")
                .WhenFalse(model => equals(model)
                    ? $"{expressionLeft} {_options.EqualsToken} {expressionRight}"
                    : $"{expressionLeft} {_options.GreaterThanToken} {expressionRight}")
                .Create();
    }

    private SpecBase<TModel, string> TransformLessThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);
        var expressionLeft = Humanize(expression.Left);
        var expressionRight = Humanize(expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => equals(model)
                    ? $"{expressionLeft} {_options.EqualsToken} {expressionRight}"
                    : $"{expressionLeft} {_options.LessThanToken} {expressionRight}")
                .WhenFalse($"{expressionLeft} {_options.GreaterThanToken} {expressionRight}")
                .Create($"{expressionLeft} {_options.LessThanOrEqualToken} {expressionRight}");
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
        ParameterExpression parameter) =>
        Spec.Build(CreatePredicate(expression, parameter))
            .WhenTrue(Humanize(Expression.Equal(expression, Expression.Constant(true))))
            .WhenFalse(Humanize(Expression.Equal(expression, Expression.Constant(false))))
            .Create();

    private SpecBase<TModel, string> CreateEqualitySpec(
        BinaryExpression expression,
        ParameterExpression parameter,
        string comparisonSymbol,
        string inverseComparisonSymbol)
    {
        var predicate = CreatePredicate(expression, parameter);
        var expressionLeft = Humanize(expression.Left);
        var expressionRight = Humanize(expression.Right);
        return
            Spec.Build(predicate)
                .WhenTrue($"{expressionLeft} {comparisonSymbol} {expressionRight}")
                .WhenFalse($"{expressionLeft} {inverseComparisonSymbol} {expressionRight}")
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
