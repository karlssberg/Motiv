namespace Motiv.DecoratorProposition;

internal sealed class SpecDecoratorMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var booleanResult = underlyingSpec.EvaluateInternal(model);

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new SpecDecoratorMultiMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            booleanResult,
            model,
            metadataResolver,
            description);
    }
}
