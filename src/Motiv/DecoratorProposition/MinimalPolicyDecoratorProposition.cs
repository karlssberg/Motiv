using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class MinimalPolicyDecoratorProposition<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> underlyingPolicy,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingPolicy.ToEnumerable();

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var predicateResult = underlyingPolicy.IsSatisfiedBy(model);
        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(predicateResult.Value,
                predicateResult.ToEnumerable()));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                description.ToReason(predicateResult.Satisfied),
                Description.Statement));

        return new PolicyResultWithUnderlying<TMetadata, TMetadata>(
            predicateResult,
            () => predicateResult.Value,
            () => metadataTier.Value,
            () => predicateResult.Explanation,
            () => resultDescription.Value);
    }
}
