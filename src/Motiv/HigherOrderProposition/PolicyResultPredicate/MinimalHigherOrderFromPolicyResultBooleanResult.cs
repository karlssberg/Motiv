using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PolicyResultPredicate;

/// <summary>
///     Represents the result of a minimal higher-order policy-result evaluation. The causes and resolved metadata
///     are only computed when first read and cached in fields to avoid per-evaluation lazy-wrapper and closure
///     allocations.
/// </summary>
internal sealed class MinimalHigherOrderFromPolicyResultBooleanResult<TModel, TMetadata>(
    bool isSatisfied,
    PolicyResult<TModel, TMetadata>[] underlyingResults,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TMetadata>>, IEnumerable<PolicyResult<TModel, TMetadata>>> causeSelector)
    : BooleanResultBase<TMetadata>
{
    private PolicyResult<TModel, TMetadata>[] CausesInternal =>
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private IEnumerable<TMetadata> MetadataValues =>
        field ??= CausesInternal.Select(result => result.Value).ToArray();

    private IEnumerable<string> ResolvedAssertions => field ??= MetadataValues switch
    {
        IEnumerable<string> reasons => reasons,
        _ => specDescription.ToReason(Satisfied).ToEnumerable()
    };

    /// <inheritdoc />
    public override MetadataNode<TMetadata> MetadataTier => field ??=
        new MetadataNode<TMetadata>(
            MetadataValues,
            CausesInternal as IEnumerable<BooleanResultBase<TMetadata>> ?? []);

    /// <inheritdoc />
    public override Explanation Explanation => field ??=
        new Explanation(ResolvedAssertions, CausesInternal, underlyingResults);

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
        new HigherOrderResultDescription<TMetadata>(
            specDescription.ToReason(Satisfied),
            CausesInternal,
            specDescription.Statement);
}
