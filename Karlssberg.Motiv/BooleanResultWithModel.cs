namespace Karlssberg.Motiv;

/// <summary>
/// Represents a boolean result with an associated model and metadata.
/// </summary>
/// <typeparam name="TModel">The type of the associated model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class BooleanResultWithModel<TModel, TMetadata> : BooleanResultBase<TMetadata>,
    ICompositeBooleanResult<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the BooleanResultWithModel class.
    /// </summary>
    /// <param name="model">The associated model.</param>
    /// <param name="underlyingResult">The underlying boolean result.</param>
    internal BooleanResultWithModel(
        TModel model,
        BooleanResultBase<TMetadata> underlyingResult)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        UnderlyingResult = underlyingResult ?? throw new ArgumentNullException(nameof(underlyingResult));
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

    /// <summary>
    /// Gets a value indicating whether the result is satisfied.
    /// </summary>
    public override bool IsSatisfied => UnderlyingResult.IsSatisfied;

    /// <summary>
    /// Gets the underlying results of the composite boolean result.
    /// </summary>
    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.UnderlyingResults =>
        [UnderlyingResult];

    /// <summary>
    /// Gets the determinative results of the composite boolean result.
    /// </summary>
    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.DeterminativeResults =>
        [UnderlyingResult];

    /// <summary>
    /// Gathers the reasons for the result.
    /// </summary>
    /// <returns>A collection of reasons.</returns>
    public override IEnumerable<string> GatherReasons() => UnderlyingResult.GatherReasons();
}