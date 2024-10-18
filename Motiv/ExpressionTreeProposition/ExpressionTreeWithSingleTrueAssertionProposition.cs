using System.Linq.Expressions;
using Motiv.Shared;

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

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var result = _predicate.Execute(model);

        var assertion = new Lazy<string>(() =>
            result.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, result)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                result.ToEnumerable(),
                result.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value,
                result.ToEnumerable()));

        var description = new Lazy<ResultDescriptionBase>(() => new BooleanResultDescriptionWithUnderlying(
            result,
            assertion.Value,
            Description.Statement));

        return new ExpressionTreePolicyResult<string>(
            result.Satisfied,
            assertion,
            metadataTier,
            explanation,
            description);
    }
}
