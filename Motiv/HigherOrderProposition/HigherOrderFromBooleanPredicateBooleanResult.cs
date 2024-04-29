namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
    bool isSatisfied,
    Func<MetadataNode<TMetadata>> metadata,
    Func<Explanation> explanation,
    Func<string> reason)
    : BooleanResultBase<TMetadata>
{
    public override MetadataNode<TMetadata> MetadataTier => metadata();
    
    public override IEnumerable<BooleanResultBase> Underlying => Enumerable.Empty<BooleanResultBase>();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        Enumerable.Empty<BooleanResultBase<TMetadata>>();
    
    public override IEnumerable<BooleanResultBase> Causes => Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => 
        Enumerable.Empty<BooleanResultBase<TMetadata>>();
    
    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description => new BooleanResultDescription(reason());

    public override Explanation Explanation => explanation();
}