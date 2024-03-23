namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
    bool isSatisfied,
    TMetadata metadata,
    string because)
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

    public override ResultDescriptionBase Description => new BooleanResultDescription(because);

    public override Explanation Explanation => new(Assertion);
    private string Assertion => 
        metadata switch {
            string reason => reason,
            _ => because
        };
}