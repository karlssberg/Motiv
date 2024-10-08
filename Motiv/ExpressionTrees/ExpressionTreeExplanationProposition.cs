using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTrees;

internal sealed class ExpressionTreeExplanationProposition<TModel>(
    Expression<Func<TModel, bool>> expression,
    Func<TModel, BooleanResultBase<string>, string> trueBecause,
    Func<TModel, BooleanResultBase<string>, string> falseBecause,
    string statement)
    : PolicyBase<TModel, string>
{
    private readonly Lazy<ISpecDescription> _description = new (() => new SpecDescription(statement));
    public override IEnumerable<SpecBase> Underlying => UnderlyingSpec.ToEnumerable();

    private Func<TModel, bool> _predicate = expression.Compile();

    public override ISpecDescription Description => _description.Value;

    public SpecBase<TModel, string> UnderlyingSpec { get; } = expression.ToSpec();

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var satisfied = _predicate(model);

        var lazyBooleanResult = new Lazy<BooleanResultBase<string>>(() => UnderlyingSpec.IsSatisfiedBy(model));
        var assertion = new Lazy<string>(() =>
            satisfied switch
            {
                true => trueBecause(model, lazyBooleanResult.Value),
                false => falseBecause(model, lazyBooleanResult.Value)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                lazyBooleanResult.Value.ToEnumerable(),
                lazyBooleanResult.Value.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value.ToEnumerable(),
                lazyBooleanResult.Value.ToEnumerable()));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                lazyBooleanResult.Value,
                assertion.Value,
                Description.Statement));

        return new ExpressionTreePolicyResult<string>(
            satisfied,
            assertion,
            metadataTier,
            explanation,
            resultDescription);
    }
}
