using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Motiv.ExpressionTreeProposition;

internal class ExpressionTreeTransformer<TModel>(Expression<Func<TModel, bool>> lambdaExpression)
{
    internal SpecBase<TModel, string> Transform()
    {
        return Transform(lambdaExpression.Body, lambdaExpression.Parameters.First());
    }

    private SpecBase<TModel, string> Transform(
        Expression expression,
        ParameterExpression parameter)
    {
        return expression switch
        {
            UnaryExpression unaryExpression =>
                TransformUnaryExpression(unaryExpression, parameter),
            BinaryExpression binaryExpression =>
                TransformBinaryExpression(binaryExpression, parameter),
            MethodCallExpression methodCallExpression =>
                TransformMethodCallExpression(methodCallExpression, parameter),
            ConditionalExpression conditionalExpression when conditionalExpression.Type == typeof(bool) =>
                TransformBooleanConditionalExpression(conditionalExpression, parameter),
            _ => TransformQuasiProposition(expression, parameter)
        };
    }

    private SpecBase<TModel, string> TransformBinaryExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        return expression.NodeType switch
        {
            ExpressionType.And =>
                Left(expression).And(Right(expression)),
            ExpressionType.AndAlso =>
                Left(expression).AndAlso(Right(expression)),
            ExpressionType.Or =>
                Left(expression).Or(Right(expression)),
            ExpressionType.OrElse =>
                Left(expression).OrElse(Right(expression)),
            ExpressionType.ExclusiveOr =>
                Left(expression).XOr(Right(expression)),
            ExpressionType.GreaterThan =>
                TransformGreaterThanExpression(expression, parameter),
            ExpressionType.GreaterThanOrEqual =>
                TransformGreaterThanOrEqualExpression(expression, parameter),
            ExpressionType.LessThan =>
                TransformLessThanExpression(expression, parameter),
            ExpressionType.LessThanOrEqual =>
                TransformLessThanOrEqualExpression(expression, parameter),
            ExpressionType.Equal =>
                TransformEqualsExpression(expression, parameter),
            ExpressionType.NotEqual =>
                TransformNotEqualsExpression(expression, parameter),
            _ => TransformQuasiProposition(expression, parameter)
        };

        SpecBase<TModel, string> Right(BinaryExpression binaryExpression)
        {
            return Transform(binaryExpression.Right, parameter);
        }

        SpecBase<TModel, string> Left(BinaryExpression binaryExpression)
        {
            return Transform(binaryExpression.Left, parameter);
        }
    }

    private SpecBase<TModel, string> TransformMethodCallExpression(
        MethodCallExpression expression,
        ParameterExpression parameter)
    {
        return expression switch
        {
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.Any), Arguments.Count: 2 }
                when IsSpecPredicate() =>
                TransformSpecExpression(expression, CreateAnySpec),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.Any), Arguments.Count: 2 }
                when IsBooleanResultPredicate() =>
                TransformPredicateExpression(expression, CreateAnySpec),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.Any), Arguments.Count: 2 }
                when IsBooleanPredicate() && IsSimpleEnumerableRelationship() =>
                TransformBinaryPredicateExpression(expression, CreateAnySpec),

            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.All), Arguments.Count: 2 }
                when IsSpecPredicate() =>
                TransformSpecExpression(expression, CreateAllSpec),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.All), Arguments.Count: 2 }
                when IsBooleanResultPredicate() =>
                TransformPredicateExpression(expression, CreateAllSpec),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.All), Arguments.Count: 2 }
                when IsBooleanPredicate() && IsSimpleEnumerableRelationship() =>
                TransformBinaryPredicateExpression(expression, CreateAllSpec),

            _ => TransformQuasiProposition(expression, parameter)
        };

        bool IsSpecPredicate()
        {
            var predicateExpression = UnwrapConvertExpression(expression.Arguments.ElementAt(1));
            if (!predicateExpression.Type.IsGenericType)
                return false;

            var genericTypeDefinition = predicateExpression.Type.GetGenericTypeDefinition();
            return genericTypeDefinition.InheritsFrom(typeof(SpecBase<,>));
        }

        bool IsBooleanResultPredicate()
        {
            var predicateExpression = UnwrapConvertExpression(expression.Arguments.ElementAt(1));
            if (!predicateExpression.Type.IsGenericType)
                return false;

            var genericTypeDefinition = predicateExpression.Type.GetGenericTypeDefinition();

            var isFunc = genericTypeDefinition.InheritsFrom(typeof(Func<,>));
            return isFunc && DoesReturn(predicateExpression, typeof(BooleanResultBase<>));
        }

        bool IsBooleanPredicate()
        {
            var predicateExpression = UnwrapConvertExpression(expression.Arguments.ElementAt(1));
            if (!predicateExpression.Type.IsGenericType)
                return false;

            var genericTypeDefinition = predicateExpression.Type.GetGenericTypeDefinition();

            var isFunc = genericTypeDefinition.InheritsFrom(typeof(Func<,>));
            return isFunc && DoesReturn(predicateExpression, typeof(bool));
        }

        bool IsSimpleEnumerableRelationship()
        {
            var predicateExpression = UnwrapConvertExpression(expression.Arguments.ElementAt(1));
            if (!predicateExpression.Type.IsGenericType)
                return false;

            var model = predicateExpression.Type.GetGenericArguments().First();
            return typeof(IEnumerable<>).MakeGenericType(model) == parameter.Type;
        }
    }

    private SpecBase<TModel, string> TransformBooleanConditionalExpression(
        ConditionalExpression expression,
        ParameterExpression parameter)
    {
        var test = Transform(expression.Test, parameter);
        var ifTrue = Transform(expression.IfTrue, parameter);
        var ifFalse = Transform(expression.IfFalse, parameter);

        var whenConditional = expression.Humanize();

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
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var equalsExpression = Expression.Equal(expression.Left, expression.Right);
        var notEqualsExpression = Expression.NotEqual(expression.Left, expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => equalsExpression.Humanize(model, parameter))
                .WhenFalse(model => notEqualsExpression.Humanize(model, parameter))
                .Create(equalsExpression.Humanize());
    }

    private SpecBase<TModel, string> TransformNotEqualsExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var notEqualExpression = Expression.NotEqual(expression.Left, expression.Right);
        var equalExpression = Expression.Equal(expression.Left, expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => notEqualExpression.Humanize(model, parameter))
                .WhenFalse(model => equalExpression.Humanize(model, parameter))
                .Create(notEqualExpression.Humanize());
    }

    private SpecBase<TModel, string> TransformGreaterThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var greaterThanExpression = Expression.GreaterThan(expression.Left, expression.Right);
        var lessThanOrEqualExpression = Expression.LessThanOrEqual(expression.Left, expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => greaterThanExpression.Humanize(model, parameter))
                .WhenFalse(model => lessThanOrEqualExpression.Humanize(model, parameter))
                .Create(greaterThanExpression.Humanize());
    }

    private SpecBase<TModel, string> TransformGreaterThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var greaterThanOrEqualExpression = Expression.GreaterThanOrEqual(expression.Left, expression.Right);
        var lessThanExpression = Expression.LessThan(expression.Left, expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => greaterThanOrEqualExpression.Humanize(model, parameter))
                .WhenFalse(model => lessThanExpression.Humanize(model, parameter))
                .Create(greaterThanOrEqualExpression.Humanize());
    }

    private SpecBase<TModel, string> TransformLessThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var lessThanExpression = Expression.LessThan(expression.Left, expression.Right);
        var greaterThanOrEqualExpression = Expression.GreaterThanOrEqual(expression.Left, expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => lessThanExpression.Humanize(model, parameter))
                .WhenFalse(model => greaterThanOrEqualExpression.Humanize(model, parameter))
                .Create(lessThanExpression.Humanize());
    }

    private SpecBase<TModel, string> TransformLessThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var lessThanOrEqualExpression = Expression.LessThanOrEqual(expression.Left, expression.Right);
        var greaterThanExpression = Expression.GreaterThan(expression.Left, expression.Right);

        return
            Spec.Build(predicate)
                .WhenTrue(model => lessThanOrEqualExpression.Humanize(model, parameter))
                .WhenFalse(model => greaterThanExpression.Humanize(model, parameter))
                .Create(lessThanOrEqualExpression.Humanize());
    }

    private SpecBase<TModel, string> TransformUnaryExpression(
        UnaryExpression expression,
        ParameterExpression parameter)
    {
        return expression.NodeType switch
        {
            ExpressionType.Not => Transform(expression.Operand, parameter).Not(),
            ExpressionType.Convert or ExpressionType.ConvertChecked => Transform(expression.Operand, parameter),
            _ => TransformQuasiProposition(expression, parameter)
        };
    }

    private SpecBase<TModel, string> TransformSpecExpression(
        MethodCallExpression expression,
        Func<Expression, Type, object, SpecBase<TModel, string>> factory)
    {
        var args = expression.Arguments.Take(2).ToArray();
        var enumerableExpression = args[0];
        var specExpression = UnwrapConvertExpression(args[1]);

        var enumerableItemType = GetEnumerableItemType(enumerableExpression.Type)!;
        return factory(
            expression,
            enumerableItemType,
            Expression
                .Lambda(specExpression)
                .Compile()
                .DynamicInvoke()
                ?? new InvalidOperationException($"The expression {expression.Humanize()} returned null."));
    }

    private SpecBase<TModel, string> TransformPredicateExpression(
        MethodCallExpression expression,
        Func<Expression, Type, object, SpecBase<TModel, string>> factory)
    {
        var args = expression.Arguments.Take(2).ToArray();
        var enumerableExpression = args[0];
        var predicateExpression = UnwrapConvertExpression(args[1]);
        if (predicateExpression is not LambdaExpression lambdaExpression)
            throw new InvalidOperationException("Unsupported predicate type");

        var unConverted = UnConvertLambdaBody(lambdaExpression);

        var enumerableItemType = GetEnumerableItemType(enumerableExpression.Type)!;
        return factory(
            expression,
            enumerableItemType,
            unConverted);
    }

    private  SpecBase<TModel, string> TransformBinaryPredicateExpression(
        MethodCallExpression expression,
        Func<Expression, Type, object, SpecBase<TModel, string>> factory)
    {
        var args = expression.Arguments.Take(2).ToArray();
        var enumerableExpression = args[0];
        var predicateExpression = UnwrapConvertExpression(args[1]);
        if (predicateExpression is not LambdaExpression lambdaExpression)
            throw new InvalidOperationException("Unsupported predicate type");

        var unConverted = UnConvertLambdaBody(lambdaExpression);

        var enumerableItemType = GetEnumerableItemType(enumerableExpression.Type)!;
        return factory(
            expression,
            enumerableItemType,
            unConverted);
    }

    private SpecBase<IEnumerable<T>, string> TransformAnyExpressionInternal<T>(
        object argument, Expression statement)
    {
        return argument switch
        {
            SpecBase<T> spec =>
                CreateAnySatisfiedSpec(spec, statement),
            Expression<Func<T, PolicyResultBase<string>>> predicate =>
                CreateAnySatisfiedSpec(predicate, statement),
            Expression<Func<T, BooleanResultBase<string>>> predicate =>
                CreateAnySatisfiedSpec(predicate, statement),
            Expression<Func<T, bool>> predicate =>
                CreateAnySatisfiedSpec(predicate, statement),
            _ => throw new InvalidOperationException($"Unsupported predicate type: {argument.GetType()}")
        };
    }

    private static SpecBase<IEnumerable<T>, string> CreateAnySatisfiedSpec<T>(SpecBase<T> spec, Expression statement)
    {
        return Spec
            .Build(spec.ToExplanationSpec())
            .AsAnySatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private SpecBase<IEnumerable<T>, string> CreateAnySatisfiedSpec<T>(Expression<Func<T, PolicyResultBase<string>>> predicate, Expression statement)
    {
        return Spec
            .From(predicate)
            .AsAnySatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private SpecBase<IEnumerable<T>, string> CreateAnySatisfiedSpec<T>(Expression<Func<T, BooleanResultBase<string>>> predicate, Expression statement)
    {
        return Spec
            .From(predicate)
            .AsAnySatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private SpecBase<IEnumerable<T>, string> CreateAnySatisfiedSpec<T>(Expression<Func<T, bool>> predicate, Expression statement)
    {
        return Spec
            .From(predicate)
            .AsAnySatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private SpecBase<IEnumerable<T>, string> TransformAllExpressionInternal<T>(
        object argument, Expression statement)
    {
        return argument switch
        {
            SpecBase<T> spec =>
                CreateAllSatisfiedSpec(spec, statement),
            Expression<Func<T, PolicyResultBase<string>>> predicate =>
                CreateAllSatisfiedSpec(predicate, statement),
            Expression<Func<T, BooleanResultBase<string>>> predicate =>
                CreateAllSatisfiedSpec(predicate, statement),
            Expression<Func<T, bool>> predicate =>
                CreateAllSatisfiedSpec(predicate, statement),
            _ => throw new InvalidOperationException($"Unsupported predicate type: {argument.GetType()}")
        };
    }

    private SpecBase<IEnumerable<T>,string> CreateAllSatisfiedSpec<T>(SpecBase<T> spec, Expression statement)
    {
        return Spec
            .Build(spec.ToExplanationSpec())
            .AsAllSatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private SpecBase<IEnumerable<T>, string> CreateAllSatisfiedSpec<T>(Expression<Func<T, BooleanResultBase<string>>> predicate, Expression statement)
    {
        return Spec
            .From(predicate)
            .AsAllSatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private SpecBase<IEnumerable<T>, string> CreateAllSatisfiedSpec<T>(Expression<Func<T, PolicyResultBase<string>>> predicate, Expression statement)
    {
        return Spec
            .From(predicate)
            .AsAllSatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private SpecBase<IEnumerable<T>,string> CreateAllSatisfiedSpec<T>(Expression<Func<T, bool>> predicate, Expression statement)
    {
        return Spec
            .From(predicate)
            .AsAllSatisfied()
            .Create(CreateExpressionStatement<T>(statement));
    }

    private static Expression CreateExpressionStatement<T>(Expression statement)
    {
        return Expression.Convert(statement, typeof(object));
    }

    private static Type? GetEnumerableItemType(Type? type)
    {
        if (type == null)
            return null;

        // Handle array types
        if (type.IsArray)
            return type.GetElementType();

        // Handle IEnumerable<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        // Search for IEnumerable<T> in interfaces
        var enumerableType = type.GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableType != null)
            return enumerableType.GetGenericArguments()[0];

        // Handle non-generic IEnumerable
        return typeof(IEnumerable).IsAssignableFrom(type)
            ? typeof(object)
            : null;
    }

    private SpecBase<TModel, string> TransformQuasiProposition(
        Expression expression,
        ParameterExpression parameter)
    {
        var (expressionAsBooleanResult, returnType) = ResolvePredicateResultExpression(expression);
        if (returnType == PredicateReturnType.BooleanResult)
        {
            var statement = Expression.Equal(
                Expression.MakeMemberAccess(
                    expression,
                    typeof(BooleanResultBase).GetProperty(nameof(BooleanResultBase.Satisfied))!),
                Expression.Constant(true));

            return CreateSpecForBooleanResult(
                expressionAsBooleanResult,
                parameter,
                statement);
        }

        var whenTrueExpression = Expression.Equal(expression, Expression.Constant(true));
        var whenFalseExpression = Expression.Equal(expression, Expression.Constant(false));

        return CreateSpecForBoolean(expression, parameter, whenTrueExpression, whenFalseExpression);
    }

    private SpecBase<TModel, string> CreateSpecForBoolean(
        Expression expression,
        ParameterExpression parameter,
        BinaryExpression whenTrueExpression,
        BinaryExpression whenFalseExpression)
    {
        return
            Spec.Build(CreateFunc<TModel, bool>(expression, parameter))
                .WhenTrue(model => whenTrueExpression.Humanize(model, parameter))
                .WhenFalse(model => whenFalseExpression.Humanize(model, parameter))
                .Create(whenTrueExpression.Humanize());
    }

    private SpecBase<TModel, string> CreateSpecForBooleanResult(
        Expression expression,
        ParameterExpression parameter,
        BinaryExpression propositionalStatementExpression)
    {
        return
            Spec.Build(CreateFunc<TModel, BooleanResultBase<string>>(expression, parameter))
                .Create(propositionalStatementExpression.Humanize());
    }

    private static (Expression, PredicateReturnType) ResolvePredicateResultExpression(Expression expression)
    {
        return expression switch
        {
            UnaryExpression { NodeType: ExpressionType.Convert } unary when unary.Type == typeof(bool) =>
                ResolvePredicateResultExpression(unary.Operand),
            _ when IsDescendantOfBooleanResultBase(expression.Type) => (expression, PredicateReturnType.BooleanResult),
            _ when IsBoolean(expression.Type) => (expression, PredicateReturnType.Boolean),
            _ => (expression, PredicateReturnType.Unknown)
        };

        bool IsDescendantOfBooleanResultBase(Type type)
        {
            return typeof(BooleanResultBase<string>).IsAssignableFrom(type);
        }

        bool IsBoolean(Type type)
        {
            return type == typeof(bool);
        }
    }

    private static MethodInfo AnyFactoryMethodInfo { get; } =
        typeof(ExpressionTreeTransformer<TModel>)
            .GetMethod(
                nameof(TransformAnyExpressionInternal),
                BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static MethodInfo AllFactoryMethodInfo { get; } =
        typeof(ExpressionTreeTransformer<TModel>)
            .GetMethod(
                nameof(TransformAllExpressionInternal),
                BindingFlags.NonPublic | BindingFlags.Instance)!;

    private SpecBase<TModel, string> CreateAnySpec(Expression expression, Type itemType, object argument) =>
            (SpecBase<TModel, string>)(AnyFactoryMethodInfo.MakeGenericMethod(itemType).Invoke(this, [argument, expression])
                                       ?? throw new InvalidOperationException(
                                           $"The factory method {AnyFactoryMethodInfo.Name} returned null."));


    private SpecBase<TModel, string> CreateAllSpec(Expression expression, Type itemType, object argument) =>
            (SpecBase<TModel, string>)(AllFactoryMethodInfo.MakeGenericMethod(itemType).Invoke(this, [argument, expression])
                                       ?? throw new InvalidOperationException(
                                           $"The factory method {AllFactoryMethodInfo.Name} returned null."));

    private static Expression UnwrapConvertExpression(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unaryExpression)
            return unaryExpression.Operand;

        return expression;
    }

    private static Func<TValue, TReturn> CreateFunc<TValue, TReturn>(
        Expression expression,
        ParameterExpression parameter)
    {
        return Expression
            .Lambda<Func<TValue, TReturn>>(expression, parameter)
            .Compile();
    }

    private static bool DoesReturn(Expression predicateExpression, Type type)
    {
        var returnType = predicateExpression.Type.GetGenericArguments().Last();
        if (predicateExpression is LambdaExpression lambdaExpression)
            returnType = UnwrapConvertExpression(lambdaExpression.Body).Type;

        return returnType.IsGenericType
            ? returnType.GetGenericTypeDefinition().InheritsFrom(type)
            : returnType.InheritsFrom(type);
    }

    private static LambdaExpression UnConvertLambdaBody(LambdaExpression lambdaExpression)
    {
        var unConvertedBody = UnwrapConvertExpression(lambdaExpression.Body);

        return Expression.Lambda(unConvertedBody, lambdaExpression.Parameters);
    }

    private enum PredicateReturnType
    {
        Unknown,
        Boolean,
        BooleanResult
    }
}
