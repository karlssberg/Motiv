using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PolicyResultPredicate;

/// <summary>
///     Represents the result of a higher-order policy-result multi-assertion explanation evaluation. The causes,
///     evaluation, and resolved assertions are only computed when first read and cached in fields to avoid
///     per-evaluation lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult<TModel, TUnderlyingMetadata>(
    bool isSatisfied,
    PolicyResult<TModel, TUnderlyingMetadata>[] underlyingResults,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> whenTrue,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : BooleanResultBase<string>
{
    private PolicyResult<TModel, TUnderlyingMetadata>[] CausesInternal =>
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata> Evaluation =>
        field ??= new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(underlyingResults, CausesInternal);

    private IEnumerable<string> MetadataValues =>
        field ??= (Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation))?.ToArray()!;

    /// <inheritdoc />
    public override MetadataNode<string> MetadataTier => field ??=
        new MetadataNode<string>(
            MetadataValues,
            CausesInternal as IEnumerable<BooleanResultBase<string>> ?? []);

    /// <inheritdoc />
    public override Explanation Explanation => field ??=
        new Explanation(
            MetadataValues.ElseFallback(() => specDescription.ToReason(Satisfied)),
            CausesInternal,
            underlyingResults);

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithValues =>
        underlyingResults as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Causes => CausesInternal;

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<string>> CausesWithValues =>
        CausesInternal as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <inheritdoc />
    public override bool Satisfied { get; } = isSatisfied;

    /// <inheritdoc />
    public override ResultDescriptionBase Description => field ??=
        new HigherOrderResultDescription<TUnderlyingMetadata>(
            specDescription.ToReason(Satisfied),
            CausesInternal,
            specDescription.Statement);
}
