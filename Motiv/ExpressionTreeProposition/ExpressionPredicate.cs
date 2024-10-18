using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal class ExpressionPredicate<TModel, TPredicateResult>
{
    internal ExpressionPredicate(Expression<Func<TModel, TPredicateResult>> predicate)
    {
        Execute = predicate switch
        {
            Expression<Func<TModel, bool>> expr => CreatePredicate(expr),
            Expression<Func<TModel, BooleanResultBase<string>>> expr => expr.Compile(),
            Expression<Func<TModel, PolicyResultBase<string>>> expr => expr.Compile(),
            _ => throw new NotSupportedException(
                $"Unsupported predicate type: Expression<Func<TModel, {typeof(TPredicateResult).Name}>>")
        };
    }

    private Func<TModel, BooleanResultBase<string>> CreatePredicate(Expression<Func<TModel, bool>> expr)
    {
        var spec = expr.ToSpec();
        return spec.IsSatisfiedBy;
        // var compiled = expr.Compile();
        // var statement = expr.Body.Humanize();
        //
        // var whenTrueExpression = expr.Body Expression.Equal(expr.Body, Expression.Constant(true));
        // var whenTrue = whenTrueExpression.Humanize();
        //
        // var whenFalseExpression = Expression.Equal(expr.Body, Expression.Constant(false));
        // var whenFalse = whenFalseExpression.Humanize();
        //
        // return model =>
        // {
        //     var satisfied = compiled(model);
        //     var assertion = satisfied ? whenTrue : whenFalse;
        //     return new ExpressionTreeBooleanResult<string>(
        //         satisfied,
        //         new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(assertion, [])),
        //         new Lazy<Explanation>(() => new Explanation(assertion, [], [])),
        //         new Lazy<ResultDescriptionBase>(() => new BooleanResultDescription(assertion, statement)));
        // };
    }

    internal Func<TModel, BooleanResultBase<string>> Execute { get; }
}
