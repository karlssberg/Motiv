using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
///     Represents the result of an expression-tree explanation proposition evaluation. The because-resolver is only
///     invoked when the <see cref="Value" /> (or a property derived from it) is read, and all derived state is cached
///     in fields to avoid per-evaluation lazy-wrapper and closure allocations. Degenerate (null/empty/whitespace)
///     because-strings fall back to the statement-derived reason for explanation and description purposes.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="result">The underlying boolean result produced by the expression.</param>
/// <param name="becauseResolver">Resolves the because-string for the outcome from the model and result.</param>
/// <param name="expression">The expression the proposition was built from.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The type of the result the expression yields.</typeparam>
internal sealed class ExpressionTreeExplanationPropositionPolicyResult<TModel, TPredicateResult>(
    bool satisfied,
    TModel model,
    BooleanResultBase<string> result,
    Func<TModel, BooleanResultBase<string>, string> becauseResolver,
    Expression<Func<TModel, TPredicateResult>> expression,
    ISpecDescription specDescription)
    : PolicyResultBase<string>
{
    private bool _hasBecause;

    private BooleanResultBase<string>[] ResultArray => field ??= [result];

    /// <inheritdoc />
    public override string Value
    {
        get
        {
            if (!_hasBecause) { field = becauseResolver(model, result); _hasBecause = true; }
            return field;
        }
    } = default!;

    private string Assertion => field ??= Value.ElseFallback(() => specDescription.ToReason(Satisfied));

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(Value.ToEnumerable(), ResultArray);

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
    public override Explanation Explanation => field ??= new Explanation(Assertion, ResultArray, ResultArray);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new ExpressionTreeBooleanResultDescription(result, Assertion, expression, specDescription.Statement);
}
