namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents a proposition that yields custom metadata based on the result of a boolean predicate.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="metadataTier">The metadata to yield when the predicate is true.</param>
/// <param name="explanation">The explanation of the proposition.</param>
/// <param name="description">The reasons for the result.</param>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class PropositionBooleanResult<TMetadata>(
    bool satisfied,
    Lazy<MetadataNode<TMetadata>> metadataTier,
    Lazy<Explanation> explanation,
    Lazy<ResultDescriptionBase> description)
    : BooleanResultBase<TMetadata>
{

    /// <summary>
    /// Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier => metadataTier.Value;

    /// <summary>
    /// Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => [];

    /// <summary>
    /// Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => [];

    /// <summary>
    /// Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => [];

    /// <summary>
    /// Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => explanation.Value;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => description.Value;
}
