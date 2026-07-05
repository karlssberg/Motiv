using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// An proposition representing a lambda expression tree.
/// </summary>
internal sealed class ExpressionTreeWithSingleTrueAssertionProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    string trueBecause,
    Func<TModel, BooleanResultBase<string>, string> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var result = _predicate.Execute(model);

        return new ExpressionTreeWithSingleTrueAssertionPropositionPolicyResult<TModel, TPredicateResult>(
            result.Satisfied,
            model,
            result,
            trueBecause,
            whenFalse,
            expression,
            description);
    }
}
