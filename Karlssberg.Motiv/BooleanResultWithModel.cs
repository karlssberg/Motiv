namespace Karlssberg.Motiv;

/// <summary>
/// Represents a boolean result with an associated model and metadata.
/// </summary>
/// <typeparam name="TModel">The type of the associated model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class BooleanResultWithModel<TModel, TMetadata> : BooleanResultBase<TMetadata>
{
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
    /// Gets a value indicating whether the result is satisfied.
    /// </summary>
    public override bool IsSatisfied => UnderlyingResult.IsSatisfied;

    /// <summary>
    /// Gets the description of the result.
    /// </summary>
    public override string Description => UnderlyingResult.Description;

    /// <summary>
    /// Gets the reasons for the result.
    /// </summary>
    public override IEnumerable<string> Reasons => UnderlyingResult.Reasons;
}