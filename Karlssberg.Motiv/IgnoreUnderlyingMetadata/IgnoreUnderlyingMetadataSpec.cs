namespace Karlssberg.Motiv.IgnoreUnderlyingMetadata;

internal class IgnoreUnderlyingMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)  
    : SpecBase<TModel, TMetadata>
{
    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;
    public override string Description => UnderlyingSpec.Description;
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        return new IgnoreUnderlyingMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
            WrapException.IfIsSatisfiedByInvocationFails(this,
                UnderlyingSpec,
            () => UnderlyingSpec.IsSatisfiedBy(model)));
    }
}