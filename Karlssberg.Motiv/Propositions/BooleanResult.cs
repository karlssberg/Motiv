namespace Karlssberg.Motiv.Propositions;

/// <summary>Represents a boolean result with associated metadata and description.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the result.</typeparam>
public sealed class BooleanResult<TMetadata>(bool value, TMetadata metadata, string specDescription)
    : BooleanResultBase<TMetadata>
{
    private readonly string _specDescription = specDescription.ThrowIfNull(nameof(specDescription));

    public override MetadataSet<TMetadata> Metadata => new([metadata]);
    public override Cause<TMetadata> Cause => new(Metadata, Explanation.Reasons);

    internal override string DebuggerDisplay() => Description;

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => new (Description);


    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => value;

    /// <summary>Gets the description of the result.</summary>
    public override string Description =>
        metadata switch
        {
            string reason => reason,
            _ => $"'{_specDescription}' is {(Satisfied ? True : False)}"
        };
}