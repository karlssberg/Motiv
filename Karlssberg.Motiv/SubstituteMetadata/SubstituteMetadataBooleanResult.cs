namespace Karlssberg.Motiv.SubstituteMetadata;

/// <summary>
/// Represents a boolean result with substitute metadata.
/// </summary>
/// <typeparam name="TMetadata">The type of the substitute metadata.</typeparam>
public class SubstituteMetadataBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubstituteMetadataBooleanResult{TMetadata}"/> class.
    /// </summary>
    /// <param name="underlyingBooleanResult">The underlying boolean result.</param>
    /// <param name="substituteMetadata">The substitute metadata.</param>
    internal SubstituteMetadataBooleanResult(BooleanResultBase<TMetadata> underlyingBooleanResult, TMetadata substituteMetadata)
    {
        UnderlyingBooleanResult = underlyingBooleanResult;
        SubstituteMetadata = substituteMetadata;

        var description = substituteMetadata switch
        {
            string reason => reason,
            _ => underlyingBooleanResult.Description
        };
        Description = description;
        Reasons = [description];
    }

    /// <summary>
    /// Gets the substitute metadata.
    /// </summary>
    public TMetadata SubstituteMetadata { get; }

    /// <summary>
    /// Gets the underlying boolean result.
    /// </summary>
    public BooleanResultBase<TMetadata> UnderlyingBooleanResult { get; }

    /// <summary>
    /// Gets a value indicating whether the boolean result is satisfied.
    /// </summary>
    public override bool IsSatisfied => UnderlyingBooleanResult.IsSatisfied;

    /// <summary>
    /// Gets the description of the boolean result.
    /// </summary>
    public override string Description { get; }

    /// <summary>
    /// Gets the reasons for the boolean result.
    /// </summary>
    public override IEnumerable<string> Reasons { get; }
}