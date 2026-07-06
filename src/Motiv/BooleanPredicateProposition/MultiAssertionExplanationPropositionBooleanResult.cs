using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
///     Represents the result of an unnamed multi-assertion explanation proposition evaluation. The assertions
///     resolver is only invoked when the <see cref="MetadataTier" /> (or a property derived from it) is read, and
///     all derived state is cached in fields to avoid per-evaluation lazy-wrapper and closure allocations.
///     Degenerate (null/empty/whitespace) assertions fall back to the statement-derived reason.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="assertionsResolver">Resolves the assertions for the outcome from the model.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
internal sealed class MultiAssertionExplanationPropositionBooleanResult<TModel>(
    bool satisfied,
    TModel model,
    Func<TModel, IEnumerable<string>> assertionsResolver,
    ISpecDescription specDescription)
    : BooleanResultBase<string>
{
    private string Assertion => field ??= specDescription.ToReason(Satisfied);

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(assertionsResolver(model).ToArray(), []);

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
    public override Explanation Explanation =>
        field ??= new Explanation(MetadataTier.Metadata.ElseFallback(() => Assertion));

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new PropositionResultDescription(Assertion, specDescription.Statement);
}
