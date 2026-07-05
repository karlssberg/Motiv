using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.ExpressionTree;

/// <summary>
///     Represents the result of a higher-order expression-tree explanation evaluation. The causes, evaluation,
///     and resolved assertion are only computed when first read and cached in fields to avoid per-evaluation
///     lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromExpressionTreeExplanationPolicyResult<TModel>(
    bool isSatisfied,
    BooleanResult<TModel, string>[] underlyingResults,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> falseBecause,
    ISpecDescription description,
    LambdaExpression expression,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : PolicyResultBase<string>
{
    private bool _hasValue;

    /// <inheritdoc />
    public override string Value
    {
        get
        {
            if (!_hasValue) { field = Satisfied ? trueBecause(Evaluation) : falseBecause(Evaluation); _hasValue = true; }
            return field;
        }
    } = default!;

    private BooleanResult<TModel, string>[] CausesInternal =>
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private HigherOrderBooleanResultEvaluation<TModel, string> Evaluation =>
        field ??= new HigherOrderBooleanResultEvaluation<TModel, string>(underlyingResults, CausesInternal);

    private string Assertion => field ??= Value.ElseFallback(() => description.ToReason(Satisfied));

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
        new HigherOrderExpressionTreeResultDescription<string>(
            Satisfied,
            Assertion,
            expression,
            CausesInternal,
            description.Statement);
}
