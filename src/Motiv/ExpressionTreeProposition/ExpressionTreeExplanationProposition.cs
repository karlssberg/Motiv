using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionTreeExplanationProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<TModel, BooleanResultBase<string>, string> trueBecause,
    Func<TModel, BooleanResultBase<string>, string> falseBecause,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying { get; } = [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var result = _predicate.Execute(model);

        var becauseResolver =
            result.Satisfied switch
            {
                true => trueBecause,
                false => falseBecause
            };

        return new ExpressionTreeExplanationPropositionPolicyResult<TModel, TPredicateResult>(
            result.Satisfied,
            model,
            result,
            becauseResolver,
            expression,
            description);
    }
}
