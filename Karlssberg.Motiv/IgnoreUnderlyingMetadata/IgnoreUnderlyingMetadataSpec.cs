namespace Karlssberg.Motiv.IgnoreUnderlyingMetadata;

internal class IgnoreUnderlyingMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : SpecBase<TModel, TMetadata>, IHaveUnderlyingSpec<TModel, TUnderlyingMetadata>
{
    public override string Description => UnderlyingSpec.Description;
    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        return new IgnoreUnderlyingMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
            UnderlyingSpec.IsSatisfiedByOrWrapException(model));
    }
}