namespace Karlssberg.Motiv.BasicProposition;

public sealed class PropositionBooleanResult<TMetadata>(
    bool value,
    TMetadata metadata,
    string assertion,
    string reason)
    : BooleanResultBase<TMetadata>
{
    public PropositionBooleanResult(
        bool value,
        TMetadata metadata,
        string because) : this(value, metadata, because, because)
    {
    }
    
    public override MetadataTree<TMetadata> MetadataTree => new(metadata);
    
    public override IEnumerable<BooleanResultBase> Underlying =>
        Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => 
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    public override IEnumerable<BooleanResultBase> Causes =>
        Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => new (assertion);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = value;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => new PropositionResultDescription(reason);
}