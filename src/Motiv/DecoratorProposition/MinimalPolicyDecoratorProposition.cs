using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class MinimalPolicyDecoratorProposition<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> underlyingPolicy,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingPolicy];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var predicateResult = underlyingPolicy.IsSatisfiedBy(model);
        PolicyResultBase<TMetadata>[] predicateResults = [predicateResult];

        return new PolicyResultWithUnderlying<TMetadata, TMetadata>(
            predicateResult,
            () => predicateResult.Value,
            () => new MetadataNode<TMetadata>(predicateResult.Value,
                predicateResults),
            () => predicateResult.Explanation,
            () => new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                description.ToReason(predicateResult.Satisfied),
                Description.Statement));
    }
}
