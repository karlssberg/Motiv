using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

/// <summary>Represents a boolean result with an associated model and metadata.</summary>
/// <typeparam name="TModel">The type of the associated model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class BooleanResult<TModel, TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>Gets the underlying boolean result.</summary>
    private readonly BooleanResultBase<TMetadata> _underlyingResult;

    /// <summary>Initializes a new instance of the BooleanResultWithModel class.</summary>
    /// <param name="model">The associated model.</param>
    /// <param name="underlyingResult">The underlying boolean result.</param>
    internal BooleanResult(
        TModel model,
        BooleanResultBase<TMetadata> underlyingResult)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _underlyingResult = underlyingResult ?? throw new ArgumentNullException(nameof(underlyingResult));
        Satisfied = underlyingResult.Satisfied;
    }

    /// <summary>Gets the model that this result evaluated.</summary>
    public TModel Model { get; }

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => _underlyingResult.Description;

    /// <summary>
    /// Gets the explanation for the result, including any underlying explanations that led to this result.
    /// </summary>
    public override Explanation Explanation => _underlyingResult.Explanation;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; }

    /// <summary>
    /// Gets the metadata as a hierarchy of metadata nodes, with the root node representing the metadata yielded by
    /// this result's proposition.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier => _underlyingResult.MetadataTier;

    /// <summary>Gets the underlying boolean result that this result encapsulates.</summary>
    public override IEnumerable<BooleanResultBase> Underlying => _underlyingResult.ToEnumerable();

    /// <summary>
    /// Gets the underlying boolean result that led to this result, that also shares the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        _underlyingResult.ToEnumerable();

    /// <summary>
    /// Gets the causes of the result.  These are the underlying results that determined the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => _underlyingResult.ToEnumerable();

    /// <summary>
    /// Gets the causes of the result, with metadata.  These are the underlying results that determined the final
    /// result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => _underlyingResult.ToEnumerable();
}