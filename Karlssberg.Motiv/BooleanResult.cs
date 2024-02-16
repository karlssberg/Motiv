namespace Karlssberg.Motiv;

/// <summary>
/// Represents a boolean result with an associated model and metadata.
/// </summary>
/// <typeparam name="TModel">The type of the associated model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class BooleanResult<TModel, TMetadata> : BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>
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
        Causes = [UnderlyingResult];
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

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }
    public override IEnumerable<BooleanResultBase<TMetadata>> Causes { get; }

    /// <summary>
    /// Gets a value indicating whether the result is satisfied.
    /// </summary>
    public override bool Satisfied => UnderlyingResult.Satisfied;

    /// <summary>
    /// Gets the underlying results of the composite boolean result.
    /// </summary>
    IEnumerable<BooleanResultBase<TMetadata>> ILogicalOperatorResult<TMetadata>.UnderlyingResults =>
        [UnderlyingResult];

    /// <summary>
    /// Gets the determinative results of the composite boolean result.
    /// </summary>
    IEnumerable<BooleanResultBase<TMetadata>> ILogicalOperatorResult<TMetadata>.Causes =>
        [UnderlyingResult];

    /// <summary>
    /// Gathers the reasons for the result.
    /// </summary>
    /// <value>A collection of reasons.</value>
    public override IEnumerable<Reason> ReasonHierarchy => UnderlyingResult.ReasonHierarchy;
}