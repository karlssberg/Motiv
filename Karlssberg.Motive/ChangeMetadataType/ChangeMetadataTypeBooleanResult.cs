namespace Karlssberg.Motive.ChangeMetadataType;

internal class ChangeMetadataTypeBooleanResult<TMetadata, TOtherMetadata>
    : BooleanResultBase<TMetadata>, IChangeMetadataTypeBooleanResult<TMetadata>
{
    internal ChangeMetadataTypeBooleanResult(BooleanResultBase<TOtherMetadata> booleanResult, TMetadata metadata)
    {
        UnderlyingBooleanResult = booleanResult;
        Metadata = metadata;
    }

    public BooleanResultBase<TOtherMetadata> UnderlyingBooleanResult { get; }
    public TMetadata Metadata { get; }
    public override bool IsSatisfied => UnderlyingBooleanResult.IsSatisfied;
    public override string Description => UnderlyingBooleanResult.Description;
    public override IEnumerable<string> Reasons => UnderlyingBooleanResult.Reasons;
}