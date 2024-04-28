namespace Karlssberg.Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents a proposition that yields custom metadata based on the result of a boolean predicate.
/// </summary>
/// <param name="value">The value of the proposition.</param>
/// <param name="metadataTier">The metadata to yield when the predicate is true.</param>
/// <param name="explanation">The explanation of the proposition.</param>
/// <param name="reason">The reason for the proposition.</param>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class PropositionBooleanResult<TMetadata>(
    bool value,
    MetadataNode<TMetadata> metadataTier,
    Explanation explanation,
    string reason)
    : BooleanResultBase<TMetadata>
{
    /// <summary>
    /// Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier => metadataTier;

    /// <summary>
    /// Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying =>
        Enumerable.Empty<BooleanResultBase>();

    /// <summary>
    /// Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => 
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    /// <summary>
    /// Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes =>
        Enumerable.Empty<BooleanResultBase>();

    /// <summary>
    /// Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => explanation;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = value;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => new PropositionResultDescription(reason);
}