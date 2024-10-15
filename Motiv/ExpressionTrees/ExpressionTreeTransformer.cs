using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Motiv.ExpressionTrees;

internal class ExpressionTreeTransformer<TModel>
{
    private readonly ExpressionAnalyzer _expressionAnalyzer = new();

    private static MethodInfo AnyFactoryMethodInfo { get; } =
        typeof(ExpressionTreeTransformer<TModel>)
            .GetMethod(
                nameof(TransformAnyExpressionInternal),
                BindingFlags.NonPublic | BindingFlags.Static)!;

    private static MethodInfo AllFactoryMethodInfo { get; } =
        typeof(ExpressionTreeTransformer<TModel>)
            .GetMethod(
                nameof(TransformAllExpressionInternal),
                BindingFlags.NonPublic | BindingFlags.Static)!;

    internal SpecBase<TModel, string> Transform(Expression<Func<TModel, bool>> expression)
    {
        return Transform(expression.Body, expression.Parameters.First());
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
                TransformSpecExpression(expression, AnyFactoryMethodInfo),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.Any), Arguments.Count: 2 }
                when IsBooleanResultPredicate() =>
                TransformPredicateExpression(expression, AnyFactoryMethodInfo),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.Any), Arguments.Count: 2 }
                when IsBooleanPredicate() && IsSimpleEnumerableRelationship() =>
                TransformBinaryPredicateExpression(expression, AnyFactoryMethodInfo),

            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.All), Arguments.Count: 2 }
                when IsSpecPredicate() =>
                TransformSpecExpression(expression, AllFactoryMethodInfo),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.All), Arguments.Count: 2 }
                when IsBooleanResultPredicate() =>
                TransformPredicateExpression(expression, AllFactoryMethodInfo),
            { Method.DeclaringType.Name: nameof(Enumerable), Method.Name: nameof(Enumerable.All), Arguments.Count: 2 }
                when IsBooleanPredicate() && IsSimpleEnumerableRelationship() =>
                TransformBinaryPredicateExpression(expression, AllFactoryMethodInfo),

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
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        var equalsExpression = Expression.Equal(expression.Left, expression.Right);
        var notEqualsExpression = Expression.NotEqual(expression.Left, expression.Right);

        if (hasAsValueMethod)
            return
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
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var notEqualExpression = Expression.NotEqual(expression.Left, expression.Right);
        var equalExpression = Expression.Equal(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
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
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var greaterThanExpression = Expression.GreaterThan(expression.Left, expression.Right);
        var lessThanOrEqualExpression = Expression.LessThanOrEqual(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(predicate)
                    .WhenTrue(model => Humanize(greaterThanExpression, model, parameter))
                    .WhenFalse(model => Humanize(lessThanOrEqualExpression, model, parameter))
                    .Create(Humanize(greaterThanExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(greaterThanExpression))
                .WhenFalse(Humanize(lessThanOrEqualExpression))
                .Create();
    }

    private SpecBase<TModel, string> TransformGreaterThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var greaterThanOrEqualExpression = Expression.GreaterThanOrEqual(expression.Left, expression.Right);
        var lessThanExpression = Expression.LessThan(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(predicate)
                    .WhenTrue(model => Humanize(greaterThanOrEqualExpression, model, parameter))
                    .WhenFalse(model => Humanize(lessThanExpression, model, parameter))
                    .Create(Humanize(greaterThanOrEqualExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(greaterThanOrEqualExpression))
                .WhenFalse(Humanize(lessThanExpression))
                .Create(Humanize(greaterThanOrEqualExpression));
    }

    private SpecBase<TModel, string> TransformLessThanExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var lessThanExpression = Expression.LessThan(expression.Left, expression.Right);
        var greaterThanOrEqualExpression = Expression.GreaterThanOrEqual(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(predicate)
                    .WhenTrue(model => Humanize(lessThanExpression, model, parameter))
                    .WhenFalse(model => Humanize(greaterThanOrEqualExpression, model, parameter))
                    .Create(Humanize(lessThanExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(lessThanExpression))
                .WhenFalse(Humanize(greaterThanOrEqualExpression))
                .Create();
    }

    private SpecBase<TModel, string> TransformLessThanOrEqualExpression(
        BinaryExpression expression,
        ParameterExpression parameter)
    {
        var predicate = CreateFunc<TModel, bool>(expression, parameter);

        var lessThanOrEqualExpression = Expression.LessThanOrEqual(expression.Left, expression.Right);
        var greaterThanExpression = Expression.GreaterThan(expression.Left, expression.Right);

        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(predicate)
                    .WhenTrue(model => Humanize(lessThanOrEqualExpression, model, parameter))
                    .WhenFalse(model => Humanize(greaterThanExpression, model, parameter))
                    .Create(Humanize(lessThanOrEqualExpression));

        return
            Spec.Build(predicate)
                .WhenTrue(Humanize(lessThanOrEqualExpression))
                .WhenFalse(Humanize(greaterThanExpression))
                .Create(Humanize(lessThanOrEqualExpression));
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

    private static SpecBase<TModel, string> TransformSpecExpression(
        MethodCallExpression expression,
        MethodInfo factory)
    {
        var args = expression.Arguments.Take(2).ToArray();
        var enumerableExpression = args[0];
        var specExpression = UnwrapConvertExpression(args[1]);
        var itemParameter = Expression.Parameter(GetEnumerableItemType(enumerableExpression.Type)!);
        var callIsSatisfied = Expression.Call
        (specExpression,
            specExpression.Type.GetMethod(nameof(SpecBase<TModel>.IsSatisfiedBy))!,
            itemParameter);

        var enumerableItemType = GetEnumerableItemType(enumerableExpression.Type)!;
        return CallSpecFactory(
            factory,
            enumerableItemType,
            Expression.Lambda(callIsSatisfied, itemParameter).Compile());
    }

    private static SpecBase<TModel, string> TransformPredicateExpression(
        MethodCallExpression expression,
        MethodInfo factory)
    {
        var args = expression.Arguments.Take(2).ToArray();
        var enumerableExpression = args[0];
        var predicateExpression = UnwrapConvertExpression(args[1]);
        if (predicateExpression is not LambdaExpression lambdaExpression)
            throw new InvalidOperationException("Unsupported predicate type");

        var unConverted = UnConvertLambdaBody(lambdaExpression);

        var enumerableItemType = GetEnumerableItemType(enumerableExpression.Type)!;
        return CallSpecFactory(
            factory,
            enumerableItemType,
            unConverted.Compile());
    }

    private static SpecBase<TModel, string> TransformBinaryPredicateExpression(
        MethodCallExpression expression,
        MethodInfo factory)
    {
        var args = expression.Arguments.Take(2).ToArray();
        var enumerableExpression = args[0];
        var predicateExpression = UnwrapConvertExpression(args[1]);
        if (predicateExpression is not LambdaExpression lambdaExpression)
            throw new InvalidOperationException("Unsupported predicate type");

        var unConverted = UnConvertLambdaBody(lambdaExpression);

        var enumerableItemType = GetEnumerableItemType(enumerableExpression.Type)!;
        return CallSpecFactory(
            factory,
            enumerableItemType,
            unConverted);
    }

    private static SpecBase<TModel, string> CallSpecFactory(
        MethodInfo method,
        Type itemType,
        object argument)
    {
        return (SpecBase<TModel, string>)
            method
                .MakeGenericMethod(itemType)
                .Invoke(null, [argument]);
    }

    private static SpecBase<IEnumerable<T>, string> TransformAnyExpressionInternal<T>(
        object argument)
    {
        return argument switch
        {
            SpecBase<T> spec => Spec
                .Build(spec.ToExplanationSpec())
                .AsAnySatisfied()
                .Create(spec.Description.Statement),
            Func<T, BooleanResultBase<string>> booleanResultPredicate => Spec
                .Build(booleanResultPredicate)
                .AsAnySatisfied()
                .Create(booleanResultPredicate.ToString()),
            Expression<Func<T, bool>> predicate => Spec
                .From(predicate)
                .AsAnySatisfied()
                .Create(predicate.ToString()),
            _ => throw new InvalidOperationException("Unsupported predicate type")
        };
    }

    private static SpecBase<IEnumerable<T>, string> TransformAllExpressionInternal<T>(
        object argument)
    {
        return argument switch
        {
            SpecBase<T> spec => Spec
                .Build(spec.ToExplanationSpec())
                .AsAllSatisfied()
                .Create(spec.Description.Statement),
            Func<T, BooleanResultBase<string>> booleanResultPredicate => Spec
                .Build(booleanResultPredicate)
                .AsAllSatisfied()
                .Create(booleanResultPredicate.ToString()),
            Expression<Func<T, bool>> predicate => Spec
                .From(predicate)
                .AsAllSatisfied()
                .Create(predicate.ToString()),
            _ => throw new InvalidOperationException("Unsupported predicate type")
        };
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
        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(CreateFunc<TModel, bool>(expression, parameter))
                    .WhenTrue(model => Humanize(whenTrueExpression, model, parameter))
                    .WhenFalse(model => Humanize(whenFalseExpression, model, parameter))
                    .Create(Humanize(whenTrueExpression));
        return
            Spec.Build(CreateFunc<TModel, bool>(expression, parameter))
                .WhenTrue(Humanize(whenTrueExpression))
                .WhenFalse(Humanize(whenFalseExpression))
                .Create();
    }

    private SpecBase<TModel, string> CreateSpecForBooleanResult(
        Expression expression,
        ParameterExpression parameter,
        BinaryExpression propositionalStatementExpression)
    {
        var hasAsValueMethod = _expressionAnalyzer.FindAsValueArguments(expression).Any();
        if (hasAsValueMethod)
            return
                Spec.Build(CreateFunc<TModel, BooleanResultBase<string>>(expression, parameter))
                    .Create(Humanize(propositionalStatementExpression));
        return
            Spec.Build(CreateFunc<TModel, BooleanResultBase<string>>(expression, parameter))
                .Create(Humanize(propositionalStatementExpression));
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

    private static string Humanize(Expression expression)
    {
        return new CSharpExpressionSerializer().Serialize(expression);
    }

    private static string Humanize<T>(Expression expression, T parameterValue, ParameterExpression parameter)
    {
        return new CSharpExpressionSerializer<T>(parameterValue, parameter).Serialize(expression);
    }

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
        return Expression.Lambda<Func<TValue, TReturn>>(expression, parameter)
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
