namespace Karlssberg.Motive;

public record BooleanResult<TMetadata> : BooleanResultBase<TMetadata>
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

    public override void Accept<TVisitor>(TVisitor visitor)
    {
        visitor.Visit(this);
    }

    public override string ToString() => Description;
}

public sealed record BooleanResult(bool IsSatisfied, string Metadata) : BooleanResult<string>(IsSatisfied, Metadata, Metadata)
{
    public override string ToString() => Metadata;
    public override void Accept<TVisitor>(TVisitor visitor)
    {
        visitor.Visit(this);
    }
}