using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

/// <summary>
///     Represents the result of a higher-order boolean-predicate multi-assertion explanation evaluation. The
///     evaluation and resolved assertions are only computed when first read and cached in fields to avoid
///     per-evaluation lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResult<TModel>(
    bool isSatisfied,
    ModelResult<TModel>[] underlyingResults,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : BooleanResultBase<string>
{
    private HigherOrderBooleanEvaluation<TModel> Evaluation => field ??=
        new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causeSelector(Satisfied, underlyingResults).ToArray());

    private IEnumerable<string> MetadataValues =>
        field ??= (Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation)).ToArray();

    private IEnumerable<string> ResolvedAssertions =>
        field ??= MetadataValues.ElseFallback(() => specDescription.ToReason(Satisfied));

    /// <inheritdoc />
    public override MetadataNode<string> MetadataTier => field ??= new MetadataNode<string>(MetadataValues, []);

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Underlying => [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithValues => [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Causes => [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<string>> CausesWithValues => [];

    /// <inheritdoc />
    public override bool Satisfied { get; } = isSatisfied;

    /// <inheritdoc />
    public override Explanation Explanation => field ??= new Explanation(ResolvedAssertions);

    /// <inheritdoc />
    public override ResultDescriptionBase Description => field ??=
        new BooleanResultDescription(specDescription.ToReason(Satisfied), specDescription.Statement, ResolvedAssertions);
}
