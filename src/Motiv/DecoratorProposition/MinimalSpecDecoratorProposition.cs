using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class MinimalSpecDecoratorProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var predicateResult = underlyingSpec.IsSatisfiedBy(model);

        return new BooleanResultWithUnderlying<TMetadata, TMetadata>(
            predicateResult,
            () => new MetadataNode<TMetadata>(
                predicateResult.Values,
                predicateResult.ToEnumerable()),
            () => predicateResult.Explanation,
            () => new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                description.ToReason(predicateResult.Satisfied),
                Description.Statement));
    }
}
