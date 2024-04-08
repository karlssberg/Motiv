namespace Karlssberg.Motiv.BooleanResultPredicate;

internal sealed class BooleanResultPredicateBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    IEnumerable<TMetadata> metadata,
    Explanation explanation,
    string reason)
    : BooleanResultBase<TMetadata>
{
    public override bool Satisfied { get; } = booleanResult.Satisfied;
    
    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            reason);

    public override Explanation Explanation => explanation;
    
    public override MetadataTree<TMetadata> MetadataTree => new(
        metadata,
        booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        booleanResult.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
}