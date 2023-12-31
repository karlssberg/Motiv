namespace Karlssberg.Motive;

public class BooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    private readonly string _description;

    public BooleanResult(bool isSatisfied, TMetadata metadata, string description)
    {
        Throw.IfNull(description, nameof(description));
        
        _description = description;
        Metadata = metadata;
        IsSatisfied = isSatisfied;
    }

    public TMetadata Metadata { get; }
    
    public override bool IsSatisfied { get; }
    
    public override string Description => Metadata switch
    {
        string reason => reason,
        _ => $"{_description}:{IsSatisfied}"
    };

    public override IEnumerable<string> Reasons
    {
        get
        {
            yield return Description;
        }
    }
}