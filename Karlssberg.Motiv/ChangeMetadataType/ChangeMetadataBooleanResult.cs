namespace Karlssberg.Motiv.ChangeMetadataType;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class ChangeMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TMetadata metadata,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override IResultDescription Description =>
        new ChangeMetadataBooleanResultDescription<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadata,
            proposition);

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override Explanation Explanation =>
        new(Description)
        {
            Underlying = [booleanResult.Explanation]
        };
    
    public override MetadataSet<TMetadata> Metadata => new(metadata);

    public override Cause<TMetadata> Cause => new(metadata, Description)
    {
        Underlying = booleanResult.Cause switch
        {
            Cause<TMetadata> causes => causes.Underlying,
            _ => Enumerable.Empty<Cause<TMetadata>>()
        }
    };
}