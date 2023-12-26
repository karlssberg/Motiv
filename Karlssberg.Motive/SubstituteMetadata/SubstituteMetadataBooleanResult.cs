namespace Karlssberg.Motive.SubstituteMetadata;

public class SubstituteMetadataBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal SubstituteMetadataBooleanResult(BooleanResultBase<TMetadata> underlyingBooleanResult, TMetadata substituteMetadata)
    {
        UnderlyingBooleanResult = underlyingBooleanResult;
        SubstituteMetadata = substituteMetadata;
    }

    public TMetadata SubstituteMetadata { get; }

    public BooleanResultBase<TMetadata> UnderlyingBooleanResult { get; }
    
    public override bool IsSatisfied => UnderlyingBooleanResult.IsSatisfied;
    
    public override string Description => UnderlyingBooleanResult.Description;
    
    public override IEnumerable<string> Reasons => UnderlyingBooleanResult.Reasons;
}