using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class MinimalSpecDecoratorProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description => description;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var predicateResult = underlyingSpec.IsSatisfiedBy(model);

        var lazyResultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                description.ToReason(predicateResult.Satisfied),
                Description.Statement));

        var lazyMetadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(
                predicateResult.Values,
                predicateResult.ToEnumerable()));

        return new BooleanResultWithUnderlying<TMetadata, TMetadata>(
            predicateResult,
            () => lazyMetadataTier.Value,
            () => predicateResult.Explanation,
            () => lazyResultDescription.Value);
    }
}
