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
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata> Evaluation =>
        field ??= new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(underlyingResults, CausesInternal);

    private IEnumerable<TMetadata> MetadataValues =>
        field ??= (Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation)).ToArray();

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
