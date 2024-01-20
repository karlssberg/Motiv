namespace Karlssberg.Motiv.IgnoreUnderlyingMetadata;

internal class IgnoreUnderlyingMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(BooleanResultBase<TUnderlyingMetadata> underlyingBooleanResult)
    : BooleanResultBase<TMetadata>
{
    public override bool IsSatisfied => underlyingBooleanResult.IsSatisfied;
    public override string Description => underlyingBooleanResult.Description;
    public override IEnumerable<string> GatherReasons() => underlyingBooleanResult.GatherReasons();
}