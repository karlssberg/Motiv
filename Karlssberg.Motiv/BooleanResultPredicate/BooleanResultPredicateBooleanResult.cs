namespace Karlssberg.Motiv.BooleanResultPredicate;

internal sealed class BooleanResultPredicateBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    IEnumerable<TMetadata> metadata,
    IEnumerable<string> assertions,
    string reason)
    : BooleanResultBase<TMetadata>
{
    /// <inheritdoc />    
    public override bool Satisfied => booleanResult.Satisfied;

    /// <inheritdoc />    
    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            reason);

    /// <inheritdoc />    
    public override ExplanationTree ExplanationTree =>
        new(assertions)
        {
            Underlying = booleanResult.ExplanationTree.ToEnumerable()
        };

    /// <inheritdoc />    
    public override MetadataTree<TMetadata> MetadataTree => new(
        metadata,
        booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());

    /// <inheritdoc />    
    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();

    /// <inheritdoc />    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        booleanResult.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();

    /// <inheritdoc />    
    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;

    /// <inheritdoc />    
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
}