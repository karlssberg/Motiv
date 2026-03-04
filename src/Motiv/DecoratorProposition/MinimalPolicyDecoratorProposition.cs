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

        return new PolicyResultWithUnderlying<TMetadata, TMetadata>(
            predicateResult,
            () => predicateResult.Value,
            () => new MetadataNode<TMetadata>(predicateResult.Value,
                predicateResult.ToEnumerable()),
            () => predicateResult.Explanation,
            () => new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                description.ToReason(predicateResult.Satisfied),
                Description.Statement));
    }
}
