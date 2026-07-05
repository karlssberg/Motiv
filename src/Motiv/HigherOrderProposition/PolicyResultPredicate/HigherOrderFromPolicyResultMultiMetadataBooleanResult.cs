using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PolicyResultPredicate;

/// <summary>
///     Represents the result of a higher-order policy-result multi-metadata evaluation. The causes, evaluation,
///     and resolved metadata are only computed when first read and cached in fields to avoid per-evaluation
///     lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromPolicyResultMultiMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    PolicyResult<TModel, TUnderlyingMetadata>[] underlyingResults,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : BooleanResultBase<TMetadata>
{
    private PolicyResult<TModel, TUnderlyingMetadata>[] CausesInternal =>
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata> Evaluation =>
        field ??= new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(underlyingResults, CausesInternal);

    private IEnumerable<TMetadata> MetadataValues =>
        field ??= Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation);

    private string Assertion => field ??= specDescription.ToReason(Satisfied);

    /// <inheritdoc />
    public override MetadataNode<TMetadata> MetadataTier => field ??=
        new MetadataNode<TMetadata>(
            MetadataValues,
            CausesInternal as IEnumerable<BooleanResultBase<TMetadata>> ?? []);

    /// <inheritdoc />
    public override Explanation Explanation => field ??=
        new Explanation(Assertion.ToEnumerable(), CausesInternal, underlyingResults);

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Causes => CausesInternal;

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        CausesInternal as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    /// <inheritdoc />
    public override bool Satisfied { get; } = isSatisfied;

    /// <inheritdoc />
    public override ResultDescriptionBase Description => field ??=
        new HigherOrderResultDescription<TUnderlyingMetadata>(
            Assertion,
            CausesInternal,
            specDescription.Statement);
}
