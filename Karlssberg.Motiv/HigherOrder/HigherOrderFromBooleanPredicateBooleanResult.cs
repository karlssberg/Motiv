namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
    bool isSatisfied,
    TMetadata metadata,
    string assertion,
    string reason)
    : BooleanResultBase<TMetadata>
{
    public override MetadataTree<TMetadata> MetadataTree => new(metadata.ToEnumerable());
    public override IEnumerable<BooleanResultBase> Underlying => Enumerable.Empty<BooleanResultBase>();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        Enumerable.Empty<BooleanResultBase<TMetadata>>();
    
    public override IEnumerable<BooleanResultBase> Causes => Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => 
        Enumerable.Empty<BooleanResultBase<TMetadata>>();
    
    public override bool Satisfied => isSatisfied;

    public override ResultDescriptionBase Description => new BooleanResultDescription(reason);

    public override ExplanationTree ExplanationTree => new(assertion)
    {
        Underlying = Enumerable.Empty<ExplanationTree>()
    };
}