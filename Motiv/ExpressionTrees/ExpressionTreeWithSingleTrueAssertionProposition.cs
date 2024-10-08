using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTrees;

/// <summary>
/// An proposition representing a lambda expression tree.
/// </summary>
internal sealed class ExpressionTreeWithSingleTrueAssertionProposition<TModel>(
    Expression<Func<TModel, bool>> expression,
    string trueBecause,
    Func<TModel, BooleanResultBase<string>, string> whenFalse,
    string? propositionalStatement = null)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => UnderlyingSpec.ToEnumerable();

    private readonly Func<TModel, bool> _predicate = expression.Compile();
    public SpecBase<TModel, string> UnderlyingSpec { get; } = expression.ToSpec();

    public override ISpecDescription Description =>
        new SpecDescription(
            propositionalStatement ?? trueBecause,
            UnderlyingSpec.Description);

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var satisfied = _predicate(model);
        var lazyBooleanResult = new Lazy<BooleanResultBase<string>>(() => UnderlyingSpec.IsSatisfiedBy(model));

        var assertion = new Lazy<string>(() =>
            satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, lazyBooleanResult.Value)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                lazyBooleanResult.Value.ToEnumerable(),
                lazyBooleanResult.Value.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value,
                lazyBooleanResult.Value.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []));

        var description = new Lazy<ResultDescriptionBase>(() => new BooleanResultDescriptionWithUnderlying(
            lazyBooleanResult.Value,
            assertion.Value,
            Description.Statement));

        return new ExpressionTreePolicyResult<string>(
            satisfied,
            assertion,
            metadataTier,
            explanation,
            description);
    }
}
