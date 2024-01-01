namespace Karlssberg.Motive.ChangeMetadataType;

internal class ChangeMetadataTypeBooleanResult<TMetadata, TOtherMetadata>(BooleanResultBase<TOtherMetadata> booleanResult, TMetadata metadata)
    : BooleanResultBase<TMetadata>, IChangeMetadataTypeBooleanResult<TMetadata>
{
    public BooleanResultBase<TOtherMetadata> UnderlyingBooleanResult => booleanResult;
    public TMetadata Metadata => metadata;
    public override bool IsSatisfied => UnderlyingBooleanResult.IsSatisfied;
    public override string Description => UnderlyingBooleanResult.Description;
    public Type OriginalMetadataType => typeof(TOtherMetadata);
    public override IEnumerable<string> Reasons => UnderlyingBooleanResult.Reasons;
}