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
    public override bool Satisfied { get; } = booleanResult.Satisfied;

    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            assertion);

    public override Explanation Explanation => booleanResult.Explanation;
    
    public override MetadataTree<TMetadata> MetadataTree => new(metadata);

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.Underlying;
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => 
        booleanResult switch
        {
            BooleanResultBase<TMetadata> result=> result.ToEnumerable(),
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;
    
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
}