namespace Karlssberg.Motive.SubstituteMetadata;

public class SubstituteMetadataBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal SubstituteMetadataBooleanResult(BooleanResultBase<TMetadata> underlyingBooleanResult, TMetadata substituteMetadata)
    {
        UnderlyingBooleanResult = underlyingBooleanResult;
        SubstituteMetadata = substituteMetadata;
        
        var description = substituteMetadata switch
        {
            string reason => reason,
            _ => underlyingBooleanResult.Description
        };
        Description = description;
        Reasons = [description];
    }

    public TMetadata SubstituteMetadata { get; }

    public BooleanResultBase<TMetadata> UnderlyingBooleanResult { get; }
    
    public override bool IsSatisfied => UnderlyingBooleanResult.IsSatisfied;
    
    public override string Description { get; } 
    
    public override IEnumerable<string> Reasons { get; }
}