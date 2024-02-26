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
    public override IAssertion Assertion =>
        new ChangeMetadataBooleanAssertion<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadata,
            proposition);

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override Reason Reason =>
        new(Assertion)
        {
            Underlying = [booleanResult.Reason]
        };
    
    public override MetadataSet<TMetadata> Metadata => new(metadata);

    public override CausalMetadata<TMetadata> CausalMetadata => new(metadata, Assertion)
    {
        Underlying = booleanResult.CausalMetadata switch
        {
            CausalMetadata<TMetadata> causes => causes.Underlying,
            _ => Enumerable.Empty<CausalMetadata<TMetadata>>()
        }
    };
}