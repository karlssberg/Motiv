namespace Karlssberg.Motiv;

public class BooleanResult(bool satisfied, string assertion) : BooleanResultBase<string>
{
    public override bool Satisfied { get; } = satisfied;
    public override ResultDescriptionBase Description { get; } =
        new BooleanResultDescription(assertion);
    public override Explanation Explanation { get; } = new (assertion);
    public override IEnumerable<BooleanResultBase> Causes { get; } =
        Enumerable.Empty<BooleanResultBase>();
    public override IEnumerable<BooleanResultBase> Underlying { get; } = 
        Enumerable.Empty<BooleanResultBase>();
    public override MetadataTree<string> MetadataTree { get; } = new(assertion);
    public override IEnumerable<BooleanResultBase<string>> CausesWithMetadata { get; } =
        Enumerable.Empty<BooleanResultBase<string>>();
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithMetadata { get; } =
        Enumerable.Empty<BooleanResultBase<string>>();
}

public class BooleanResult<TMetadata>(
    bool satisfied, 
    string assertion, 
    TMetadata metadata) : BooleanResultBase<TMetadata>
{
    public override bool Satisfied { get; } = satisfied;
    
    public override ResultDescriptionBase Description { get; } =
        new BooleanResultDescription(assertion);
    
    public override Explanation Explanation { get; } = new (assertion);
    
    public override IEnumerable<BooleanResultBase> Causes { get; }  =
        Enumerable.Empty<BooleanResultBase>();
    
    public override IEnumerable<BooleanResultBase> Underlying { get; } =
        Enumerable.Empty<BooleanResultBase>();
    
    public override MetadataTree<TMetadata> MetadataTree { get; } = new(metadata);
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata { get; } =
        Enumerable.Empty<BooleanResultBase<TMetadata>>();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata { get; } = 
        Enumerable.Empty<BooleanResultBase<TMetadata>>();
}