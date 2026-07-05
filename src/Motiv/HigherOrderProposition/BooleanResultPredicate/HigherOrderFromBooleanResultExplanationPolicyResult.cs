using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanResultPredicate;

/// <summary>
///     Represents the result of a higher-order boolean-result explanation policy evaluation. The causes,
///     evaluation, and resolved assertion are only computed when first read and cached in fields to avoid
///     per-evaluation lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromBooleanResultExplanationPolicyResult<TModel, TUnderlyingMetadata>(
    bool isSatisfied,
    BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, string> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : PolicyResultBase<string>
{
    private bool _hasValue;

    /// <inheritdoc />
    public override string Value
    {
        get
        {
            if (!_hasValue) { field = Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation); _hasValue = true; }
            return field;
        }
    } = default!;

    private BooleanResult<TModel, TUnderlyingMetadata>[] CausesInternal =>
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata> Evaluation =>
        field ??= new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(underlyingResults, CausesInternal);

    private string Assertion => field ??= Value.ElseFallback(() => specDescription.ToReason(Satisfied));

    /// <inheritdoc />
    public override MetadataNode<string> MetadataTier => field ??=
        new MetadataNode<string>(
            Value.ToEnumerable(),
            CausesInternal as IEnumerable<BooleanResultBase<string>> ?? []);

    /// <inheritdoc />
    public override Explanation Explanation => field ??=
        new Explanation(Assertion.ToEnumerable(), CausesInternal, underlyingResults);

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
            Assertion,
            CausesInternal,
            specDescription.Statement);
}
