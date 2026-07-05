using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

/// <summary>
///     Represents the result of a higher-order boolean-predicate explanation evaluation. The evaluation and
///     resolved assertion are only computed when first read and cached in fields to avoid per-evaluation
///     lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromBooleanPredicateExplanationPolicyResult<TModel>(
    bool isSatisfied,
    ModelResult<TModel>[] underlyingResults,
    Func<HigherOrderBooleanEvaluation<TModel>, string> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, string> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
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

    private HigherOrderBooleanEvaluation<TModel> Evaluation => field ??=
        new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causeSelector(Satisfied, underlyingResults).ToArray());

    private string Assertion => field ??= Value.ElseFallback(() => specDescription.ToReason(Satisfied));

    /// <inheritdoc />
    public override MetadataNode<string> MetadataTier => field ??= new MetadataNode<string>(Value);

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
    public override Explanation Explanation => field ??= new Explanation(Assertion);

    /// <inheritdoc />
    public override ResultDescriptionBase Description => field ??=
        new BooleanResultDescription(Assertion, specDescription.Statement);
}
