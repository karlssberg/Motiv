using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
///     Represents the result of an expression-tree metadata proposition evaluation. The metadata resolver is only
///     invoked when the <see cref="Value" /> (or a property derived from it) is read, and all derived state is
///     cached in fields to avoid per-evaluation lazy-wrapper and closure allocations. When the resolved metadata is
///     itself a sequence of strings it supplies the assertions; otherwise the assertions are co-opted from the
///     underlying result.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="result">The underlying boolean result produced by the expression.</param>
/// <param name="metadataResolver">Resolves the metadata for the outcome from the model and result.</param>
/// <param name="expression">The expression the proposition was built from.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TPredicateResult">The type of the result the expression yields.</typeparam>
internal sealed class ExpressionTreeMetadataPropositionPolicyResult<TModel, TMetadata, TPredicateResult>(
    bool satisfied,
    TModel model,
    BooleanResultBase<string> result,
    Func<TModel, BooleanResultBase<string>, TMetadata> metadataResolver,
    Expression<Func<TModel, TPredicateResult>> expression,
    ISpecDescription specDescription)
    : PolicyResultBase<TMetadata>
{
    private bool _hasValue;

    private BooleanResultBase<string>[] ResultArray => field ??= [result];

    /// <inheritdoc />
    public override TMetadata Value
    {
        get
        {
            if (!_hasValue) { field = metadataResolver(model, result); _hasValue = true; }
            return field;
        }
    } = default!;

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier =>
        field ??= new MetadataNode<TMetadata>(Value,
            ResultArray as IEnumerable<BooleanResultBase<TMetadata>> ?? []);

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
    public override Explanation Explanation => field ??= new Explanation(ResolveAssertions(), ResultArray, ResultArray);

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
        Value switch
        {
            IEnumerable<string> because => because.ToArray(),
            _ => result.Assertions
        };
}
