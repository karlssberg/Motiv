namespace Karlssberg.Motive;

public class BooleanResult<TMetadata> : BooleanResultBase<TMetadata>, IBooleanResult<TMetadata>
{
    public BooleanResult(bool isSatisfied, TMetadata metadata, string description)
    {
        Throw.IfNull(description, nameof(description));
        
        Metadata = metadata;
        IsSatisfied = isSatisfied;
        Description = Metadata switch
        {
            string reason => reason,
            _ => $"{description}:{isSatisfied}"
        };
    }

    public TMetadata Metadata { get; }
    
    public override bool IsSatisfied { get; }
    
    public override string Description { get; }

    public override IEnumerable<string> Reasons
    {
        get
        {
            yield return Description;
        }
    }
}