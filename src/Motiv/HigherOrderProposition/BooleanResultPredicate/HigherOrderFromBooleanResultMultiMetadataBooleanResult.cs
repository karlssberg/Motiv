using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanResultPredicate;

/// <summary>
///     Represents the result of a higher-order boolean-result multi-metadata evaluation. The causes, evaluation,
///     and resolved metadata are only computed when first read and cached in fields to avoid per-evaluation
///     lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromBooleanResultMultiMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : BooleanResultBase<TMetadata>
{
    private BooleanResult<TModel, TUnderlyingMetadata>[] CausesInternal =>
        field ??= HigherOrderResults.ResolveCauses(Satisfied, underlyingResults, causeSelector);

    // Stryker disable once Assignment : equivalent — read only through MetadataValues by the memoised MetadataTier, so it is built once either way (#294)
    private HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata> Evaluation =>
        field ??= new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(underlyingResults, CausesInternal);

    private IEnumerable<TMetadata> MetadataValues =>
        HigherOrderResults.ResolveValues(Satisfied, Evaluation, whenTrue, whenFalse);

    // Stryker disable once Assignment : equivalent — read only by the memoised Explanation and Description, so a rebuild only gives the second an equal copy (#294)
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
