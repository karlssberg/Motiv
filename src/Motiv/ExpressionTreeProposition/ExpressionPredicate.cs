using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal class ExpressionPredicate<TModel, TPredicateResult>
{
    internal ExpressionPredicate(Expression<Func<TModel, TPredicateResult>> predicate)
    {
        switch (predicate)
        {
            case Expression<Func<TModel, bool>> expr:
                var spec = expr.ToSpec();
                Execute = spec.Evaluate;
                Match = spec.Matches;
                break;
            case Expression<Func<TModel, BooleanResultBase<string>>> expr:
                var compiled = expr.Compile();
                Execute = compiled;
                Match = model => compiled(model).Satisfied;
                break;
            case Expression<Func<TModel, PolicyResultBase<string>>> expr:
                var compiledPolicy = expr.Compile();
                Execute = compiledPolicy;
                Match = model => compiledPolicy(model).Satisfied;
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported predicate type: Expression<Func<TModel, {typeof(TPredicateResult).Name}>>");
        }
    }

    internal Func<TModel, BooleanResultBase<string>> Execute { get; }

    internal Func<TModel, bool> Match { get; }
}
