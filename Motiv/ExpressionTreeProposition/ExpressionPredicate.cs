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
    }

    internal Func<TModel, BooleanResultBase<string>> Execute { get; }
}
