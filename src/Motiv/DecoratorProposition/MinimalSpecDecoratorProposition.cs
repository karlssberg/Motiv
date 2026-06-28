using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class MinimalSpecDecoratorProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var predicateResult = underlyingSpec.Evaluate(model);
        BooleanResultBase<TMetadata>[] predicateResults = [predicateResult];

        return new BooleanResultWithUnderlying<TMetadata, TMetadata>(
            predicateResult,
            () => new MetadataNode<TMetadata>(
                predicateResult.Values,
                predicateResults),
            () => predicateResult.Explanation,
            () => new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                description.ToReason(predicateResult.Satisfied),
                Description.Statement));
    }
}
