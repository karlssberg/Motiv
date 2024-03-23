namespace Karlssberg.Motiv.Composite;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class CompositeMultiMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    MetadataTree<TMetadata> metadataTree,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    /// <inheritdoc />
    public override bool Satisfied => booleanResult.Satisfied;

    /// <inheritdoc />
    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            proposition.ToReason(booleanResult.Satisfied));

    /// <inheritdoc />
    public override Explanation Explanation =>
        new(ResolveAssertions())
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

    /// <inheritdoc />
    public override MetadataTree<TMetadata> MetadataTree => metadataTree;
    
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
    
    private IEnumerable<string> ResolveAssertions() => 
        metadataTree switch {
            MetadataTree<string>  reasons => reasons,
            _ => proposition.ToReason(booleanResult.Satisfied).ToEnumerable()
        };
}