using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

/// <summary>
///     Represents the result of a higher-order boolean-predicate multi-metadata evaluation. The evaluation and
///     resolved metadata are only computed when first read and cached in fields to avoid per-evaluation
///     lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromBooleanPredicateMultiMetadataBooleanResult<TModel, TMetadata>(
    bool isSatisfied,
    ModelResult<TModel>[] underlyingResults,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : BooleanResultBase<TMetadata>
{
    private HigherOrderBooleanEvaluation<TModel> Evaluation => field ??=
        new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causeSelector(Satisfied, underlyingResults).ToArray());

    private IEnumerable<TMetadata> MetadataValues =>
        field ??= (Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation)).ToArray();

    private IEnumerable<string> ResolvedAssertions => field ??= specDescription.ToReason(Satisfied).ToEnumerable();

    /// <inheritdoc />
    public override MetadataNode<TMetadata> MetadataTier => field ??= new MetadataNode<TMetadata>(MetadataValues, []);

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Underlying => [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Causes => [];

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => [];

    /// <inheritdoc />
    public override bool Satisfied { get; } = isSatisfied;

    /// <inheritdoc />
    public override Explanation Explanation => field ??= new Explanation(ResolvedAssertions);

    /// <inheritdoc />
    public override ResultDescriptionBase Description => field ??=
        new BooleanResultDescription(specDescription.ToReason(Satisfied), specDescription.Statement, ResolvedAssertions);
}
