namespace Karlssberg.Motiv.ChangeMetadataType;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class ChangeMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TMetadata metadata,
    string assertion)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            assertion);

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override ExplanationTree ExplanationTree =>
        new(Description)
        {
            Underlying = booleanResult.ExplanationTree.ToEnumerable()
        };
    
    /// <inheritdoc />
    public override MetadataTree<TMetadata> MetadataTree => new(metadata);
    
    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();
    
    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => 
        booleanResult switch
        {
            BooleanResultBase<TMetadata> result=> result.ToEnumerable(),
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;
    
    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
}