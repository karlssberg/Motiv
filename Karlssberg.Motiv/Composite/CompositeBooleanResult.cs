namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TMetadata metadata,
    string because)
    : BooleanResultBase<TMetadata>
{
    /// <inheritdoc />
    public override bool Satisfied => booleanResult.Satisfied;

    /// <inheritdoc />
    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            because);

    /// <inheritdoc />
    public override Explanation Explanation =>
        new(because)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

    /// <inheritdoc />
    public override MetadataTree<TMetadata> MetadataTree => new(
        metadata, 
        booleanResult.ResolveMetadataSets<TMetadata, TUnderlyingMetadata>());
    
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