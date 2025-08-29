using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
///     Represents a proposition that yields custom metadata based on the result of a boolean predicate.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="assertion">A truth about the result.</param>
/// <param name="description">The description of the proposition result.</param>
internal sealed class MinimalPropositionPolicyResult(
    bool satisfied,
    string assertion,
    ResultDescriptionBase description)
    : PolicyResultBase<string>
{
    /// <inheritdoc />
    public override string Value => assertion;

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<string> MetadataTier => new(assertion, []);

    /// <summary>
    ///     Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => [];

    /// <summary>
    ///     Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithValues => [];

    /// <summary>
    ///     Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => [];

    /// <summary>
    ///     Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<string>> CausesWithValues => [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => new(assertion);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => description;
}
