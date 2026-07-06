using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
///     Represents the result of a multi-value metadata proposition evaluation. The metadata resolver is only
///     invoked when the <see cref="MetadataTier" /> is read, and all derived state is cached in fields to avoid
///     per-evaluation lazy-wrapper and closure allocations.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="metadataResolver">Resolves the metadata collection for the outcome from the model.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class MultiValuePropositionBooleanResult<TModel, TMetadata>(
    bool satisfied,
    TModel model,
    Func<TModel, IEnumerable<TMetadata>> metadataResolver,
    ISpecDescription specDescription)
    : BooleanResultBase<TMetadata>
{
    private string Assertion => field ??= specDescription.ToReason(Satisfied);

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier =>
        field ??= new MetadataNode<TMetadata>(metadataResolver(model)?.ToArray()!, []);

    /// <summary>
    ///     Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => [];

    /// <summary>
    ///     Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => [];

    /// <summary>
    ///     Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => [];

    /// <summary>
    ///     Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => field ??= new Explanation(Assertion.ToEnumerable());

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new PropositionResultDescription(Assertion, specDescription.Statement);
}
