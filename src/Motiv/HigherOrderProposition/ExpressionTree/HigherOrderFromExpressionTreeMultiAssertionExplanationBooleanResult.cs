using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.ExpressionTree;

/// <summary>
///     Represents the result of a higher-order expression-tree multi-assertion explanation evaluation. The causes,
///     evaluation, and resolved assertions are only computed when first read and cached in fields to avoid
///     per-evaluation lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult<TModel>(
    bool isSatisfied,
    BooleanResult<TModel, string>[] underlyingResults,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> trueBecause,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> falseBecause,
    ISpecDescription description,
    LambdaExpression expression,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : BooleanResultBase<string>
{
    private BooleanResult<TModel, string>[] CausesInternal =>
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private HigherOrderBooleanResultEvaluation<TModel, string> Evaluation =>
        field ??= new HigherOrderBooleanResultEvaluation<TModel, string>(underlyingResults, CausesInternal);

    private IEnumerable<string> MetadataValues =>
        field ??= Satisfied ? trueBecause(Evaluation) : falseBecause(Evaluation);

    /// <inheritdoc />
    public override MetadataNode<string> MetadataTier => field ??=
        new MetadataNode<string>(
            MetadataValues,
            CausesInternal as IEnumerable<BooleanResultBase<string>> ?? []);

    /// <inheritdoc />
    public override Explanation Explanation => field ??=
        new Explanation(
            MetadataValues.ElseFallback(() => underlyingResults.GetAssertions()),
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
        new HigherOrderExpressionTreeResultDescription<string>(
            Satisfied,
            description.ToReason(Satisfied),
            expression,
            CausesInternal,
            description.Statement);
}
