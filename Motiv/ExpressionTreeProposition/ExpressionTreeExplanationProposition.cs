using System.Linq.Expressions;
using Motiv.Shared;

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

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var result = _predicate.Execute(model);

        var assertion = new Lazy<string>(() =>
            result.Satisfied switch
            {
                true => trueBecause(model, result),
                false => falseBecause(model, result)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                result.ToEnumerable(),
                result.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value.ToEnumerable(),
                result.ToEnumerable()));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                result,
                assertion.Value,
                Description.Statement));

        return new ExpressionTreePolicyResult<string>(
            result.Satisfied,
            assertion,
            metadataTier,
            explanation,
            resultDescription);
    }
}
