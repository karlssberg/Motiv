namespace Karlssberg.Motiv.Propositions;

/// <summary>Represents a boolean result with associated metadata and description.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the result.</typeparam>
public sealed class BooleanResult<TMetadata>(
    bool value,
    TMetadata metadata,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    public override MetadataSet<TMetadata> Metadata => new([metadata]);
    public override CausalMetadata<TMetadata> CausalMetadata => new(Metadata, Reason.Assertions);

    /// <summary>Gets the reasons for the result.</summary>
    public override Reason Reason => new(Assertion)
    {
        Underlying = Enumerable.Empty<Reason>()
    };

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => value;

    /// <summary>Gets the description of the result.</summary>
    public override IAssertion Assertion =>
        new Assertion<TMetadata>(value, proposition, metadata);
}