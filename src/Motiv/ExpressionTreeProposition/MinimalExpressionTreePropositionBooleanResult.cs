using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
///     Represents the result of a minimal expression-tree proposition evaluation. The assertions resolver is only
///     invoked when the <see cref="MetadataTier" /> or <see cref="Explanation" /> is read, its result is cached so it
///     runs at most once, and all derived state is cached in fields to avoid per-evaluation lazy-wrapper and closure
///     allocations. When the resolved metadata is a sequence of strings it supplies the assertions; otherwise the
///     assertions are co-opted from the underlying result.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="result">The underlying boolean result produced by the expression.</param>
/// <param name="assertionsResolver">Resolves the assertions for the outcome from the model and result.</param>
/// <param name="expression">The expression the proposition was built from.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The type of the result the expression yields.</typeparam>
internal sealed class MinimalExpressionTreePropositionBooleanResult<TModel, TPredicateResult>(
    bool satisfied,
    TModel model,
    BooleanResultBase<string> result,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> assertionsResolver,
    Expression<Func<TModel, TPredicateResult>> expression,
    ISpecDescription specDescription)
    : BooleanResultBase<string>
{
    private BooleanResultBase<string>[] ResultArray => field ??= [result];

    private IEnumerable<string> MetadataResults => field ??= assertionsResolver(model, result);

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(MetadataResults,
            ResultArray as IEnumerable<BooleanResultBase<string>> ?? []);

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
    public override Explanation Explanation => field ??= new Explanation(ResolveAssertions(), result);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new ExpressionTreeBooleanResultDescription(
            result,
            specDescription.ToReason(Satisfied),
            expression,
            specDescription.Statement);

    private IEnumerable<string> ResolveAssertions() =>
        MetadataResults switch
        {
            IEnumerable<string> because => because,
            _ => result.Assertions
        };
}
