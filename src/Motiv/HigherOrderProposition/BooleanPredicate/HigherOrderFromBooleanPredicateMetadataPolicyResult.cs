using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

/// <summary>
///     Represents the result of a higher-order boolean-predicate metadata evaluation. The evaluation and resolved
///     metadata are only computed when first read and cached in fields to avoid per-evaluation lazy-wrapper and
///     closure allocations.
/// </summary>
internal sealed class HigherOrderFromBooleanPredicateMetadataPolicyResult<TModel, TMetadata>(
    bool isSatisfied,
    ModelResult<TModel>[] underlyingResults,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : PolicyResultBase<TMetadata>
{
    private bool _hasValue;

    /// <inheritdoc />
    public override TMetadata Value
    {
        get
        {
            if (!_hasValue) { field = Satisfied ? whenTrue(Evaluation) : whenFalse(Evaluation); _hasValue = true; }
            return field;
        }
    } = default!;

    private HigherOrderBooleanEvaluation<TModel> Evaluation => field ??=
        new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causeSelector(Satisfied, underlyingResults).ToArray());

    private string Assertion => field ??= specDescription.ToReason(Satisfied);

    /// <inheritdoc />
    public override MetadataNode<TMetadata> MetadataTier => field ??= new MetadataNode<TMetadata>(Value);

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
    public override Explanation Explanation => field ??= new Explanation(Assertion);

    /// <inheritdoc />
    public override ResultDescriptionBase Description => field ??=
        new BooleanResultDescription(Assertion, specDescription.Statement);
}
