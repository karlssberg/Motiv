using System.Linq.Expressions;

namespace Motiv.ExpressionTrees;

internal class ExpressionTreeTransformer<TModel>()
{
    private readonly ExpressionAnalyzer _expressionAnalyzer = new ();

    internal SpecBase<TModel, string> Transform(Expression<Func<TModel, bool>> expression) =>
        Transform(expression.Body, expression.Parameters.First());

    internal SpecBase<TModel, string> Transform(Expression<Func<TModel, BooleanResultBase<string>>> expression) =>
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

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        var equalsExpression = Expression.Equal(expression.Left, expression.Right);
        var notEqualsExpression = Expression.NotEqual(expression.Left, expression.Right);

        if (hasAsValueMethod)
            Spec.Build(predicate)
                .WhenTrue(model => Humanize(equalsExpression, model, parameter))
                .WhenFalse(model => Humanize(notEqualsExpression, model, parameter))
                .Create(Humanize(equalsExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(equalsExpression))
                .WhenFalse(Humanize(notEqualsExpression))
                .Create();
    }

    private SpecBase<TModel, string> TransformNotEqualsExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);

        var notEqualExpression = Expression.NotEqual(expression.Left, expression.Right);
        var equalExpression = Expression.Equal(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            Spec.Build(predicate)
                .WhenTrue(model => Humanize(notEqualExpression, model, parameter))
                .WhenFalse(model => Humanize(equalExpression, model, parameter))
                .Create(Humanize(notEqualExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(notEqualExpression))
                .WhenFalse(Humanize(equalExpression))
                .Create();
    }

    private SpecBase<TModel, string> TransformGreaterThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var greaterThanExpression = Expression.GreaterThan(expression.Left, expression.Right);
        var equalsExpression = Expression.Equal(expression.Left, expression.Right);
        var lessThanExpression = Expression.LessThan(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(predicate)
                    .WhenTrue(model => Humanize(greaterThanExpression, model, parameter))
                    .WhenFalse(model => equals(model)
                        ? Humanize(equalsExpression, model, parameter)
                        : Humanize(lessThanExpression, model, parameter))
                    .Create(Humanize(greaterThanExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(greaterThanExpression))
                .WhenFalse(model => equals(model) ? Humanize(equalsExpression) : Humanize(lessThanExpression))
                .Create();
    }

    private SpecBase<TModel, string> TransformGreaterThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var equalsExpression = Expression.Equal(expression.Left, expression.Right);
        var greaterThanExpression = Expression.GreaterThan(expression.Left, expression.Right);
        var lessThanExpression = Expression.LessThan(expression.Left, expression.Right);
        var greaterThanOrEqual = Expression.GreaterThanOrEqual(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(predicate)
                    .WhenTrue(model => equals(model)
                        ? Humanize(equalsExpression, model, parameter)
                        : Humanize(greaterThanExpression, model, parameter))
                    .WhenFalse(model => Humanize(lessThanExpression, model, parameter))
                    .Create(Humanize(greaterThanOrEqual));

        return
            Spec.Build(predicate)
                .WhenTrue(model => equals(model)
                    ? Humanize(equalsExpression)
                    : Humanize(greaterThanExpression))
                .WhenFalse(Humanize(lessThanExpression))
                .Create(Humanize(greaterThanOrEqual));
    }

    private SpecBase<TModel, string> TransformLessThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var lessThanExpression = Expression.LessThan(expression.Left, expression.Right);
        var equalsExpression = Expression.Equal(expression.Left, expression.Right);
        var greaterThanExpression = Expression.GreaterThan(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            Spec.Build(predicate)
                .WhenTrue(model => Humanize(lessThanExpression, model, parameter))
                .WhenFalse(model => equals(model)
                    ? Humanize(equalsExpression, model, parameter)
                    : Humanize(greaterThanExpression, model, parameter))
                .Create(Humanize(lessThanExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(lessThanExpression))
                .WhenFalse(model => equals(model)
                    ? Humanize(equalsExpression)
                    : Humanize(greaterThanExpression))
                .Create();
    }

    private SpecBase<TModel, string> TransformLessThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreatePredicate(expression, parameter);
        var equals = CreateEqualsFunction(expression, parameter);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            Spec.Build(predicate)
                .WhenTrue(model => equals(model)
                    ? Humanize(Expression.Equal(expression.Left, expression.Right), model, parameter)
                    : Humanize(Expression.LessThan(expression.Left, expression.Right), model, parameter))
                .WhenFalse(model => Humanize(Expression.GreaterThan(expression.Left, expression.Right), model, parameter))
                .Create(Humanize(Expression.LessThanOrEqual(expression.Left, expression.Right)));

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
        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();

        var whenTrueExpression = Expression.Equal(expression, Expression.Constant(true));
        var whenFalseExpression = Expression.Equal(expression, Expression.Constant(false));

        if(hasAsValueMethod)
            return
                Spec.Build(CreatePredicate(expression, parameter))
                    .WhenTrue(model => Humanize(whenTrueExpression, model, parameter))
                    .WhenFalse(model => Humanize(whenFalseExpression, model, parameter))
                    .Create(Humanize(expression));

        return
            Spec.Build(CreatePredicate(expression, parameter))
                .WhenTrue(Humanize(whenTrueExpression))
                .WhenFalse(Humanize(whenFalseExpression))
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

    private static string Humanize(Expression expression) =>
        new CSharpExpressionSerializer().Serialize(expression);

    private static string Humanize<T>(Expression expression, T parameterValue, ParameterExpression parameter) =>
        new CSharpExpressionSerializer<T>(parameterValue, parameter).Serialize(expression);
}
