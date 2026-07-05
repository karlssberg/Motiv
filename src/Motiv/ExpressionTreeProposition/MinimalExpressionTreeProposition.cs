using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class MinimalExpressionTreeProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> whenTrue,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying { get; } = [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override BooleanResultBase<string> EvaluateSpec(TModel model)
    {
        var result = _predicate.Execute(model);

        var assertionsResolver =
            result.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new MinimalExpressionTreePropositionBooleanResult<TModel, TPredicateResult>(
            result.Satisfied,
            model,
            result,
            assertionsResolver,
            expression,
            description);
    }
}
