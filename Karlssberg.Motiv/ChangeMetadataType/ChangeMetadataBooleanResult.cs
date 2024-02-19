namespace Karlssberg.Motiv.ChangeMetadataType;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class ChangeMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TMetadata metadata,
    string description)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description =>
        metadata switch
        {
            string reason => reason,
            _ => description
        };

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override Explanation Explanation =>
        metadata switch
        {
            string reason => new Explanation(reason)
            {
                Underlying = [booleanResult.Explanation]
            },
            _ => new Explanation(Description)
            {
                Underlying = [booleanResult.Explanation]
            }
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