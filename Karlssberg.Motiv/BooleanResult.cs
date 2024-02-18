namespace Karlssberg.Motiv;

/// <summary>
/// Represents a boolean result with an associated model and metadata.
/// </summary>
/// <typeparam name="TModel">The type of the associated model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class BooleanResult<TModel, TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the BooleanResultWithModel class.
    /// </summary>
    /// <param name="model">The associated model.</param>
    /// <param name="underlyingResult">The underlying boolean result.</param>
    internal BooleanResult(
        TModel model,
        BooleanResultBase<TMetadata> underlyingResult)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        UnderlyingResult = underlyingResult ?? throw new ArgumentNullException(nameof(underlyingResult));
        UnderlyingResults = [UnderlyingResult];
    }

    /// <summary>
    /// Gets the associated model.
    /// </summary>
    public TModel Model { get; }

    /// <summary>
    /// Gets the underlying boolean result.
    /// </summary>
    public BooleanResultBase<TMetadata> UnderlyingResult { get; }

    /// <summary>
    /// Gets the description of the result.
    /// </summary>
    public override string Description => UnderlyingResult.Description;

    public override IEnumerable<BooleanResultBase> UnderlyingResults { get; }
    public override Explanation Explanation => UnderlyingResult.Explanation;
    /// <summary>
    /// Gets a value indicating whether the result is satisfied.
    /// </summary>
    public override bool Satisfied => UnderlyingResult.Satisfied;

    public override MetadataSet<TMetadata> Metadata => UnderlyingResult.Metadata;
    public override Cause<TMetadata> Cause => UnderlyingResult.Cause;
}