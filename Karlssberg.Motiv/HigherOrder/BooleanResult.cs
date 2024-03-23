namespace Karlssberg.Motiv.HigherOrder;

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
    public BooleanResult(
        TModel model,
        BooleanResultBase<TMetadata> underlyingResult)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _underlyingResult = underlyingResult ?? throw new ArgumentNullException(nameof(underlyingResult));
    }

    /// <summary>Gets the associated model.</summary>
    public TModel Model { get; }

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => _underlyingResult.Description;

    public override ExplanationTree ExplanationTree => _underlyingResult.ExplanationTree;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => _underlyingResult.Satisfied;

    public override MetadataTree<TMetadata> MetadataTree => _underlyingResult.MetadataTree;
    public override IEnumerable<BooleanResultBase> Underlying  => _underlyingResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => _underlyingResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase> Causes => _underlyingResult.Causes;
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => _underlyingResult.CausesWithMetadata;
}