using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.ExpressionTree;

/// <summary>
///     Represents the result of a higher-order expression-tree metadata evaluation. The causes, evaluation, and
///     resolved metadata are only computed when first read and cached in fields to avoid per-evaluation
///     lazy-wrapper and closure allocations.
/// </summary>
internal sealed class HigherOrderFromExpressionTreeMetadataPolicyResult<TModel, TMetadata>(
    bool isSatisfied,
    BooleanResult<TModel, string>[] underlyingResults,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenFalse,
    ISpecDescription description,
    LambdaExpression expression,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
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

    private BooleanResult<TModel, string>[] CausesInternal =>
        field ??= causeSelector(Satisfied, underlyingResults).ToArray();

    private HigherOrderBooleanResultEvaluation<TModel, string> Evaluation =>
        field ??= new HigherOrderBooleanResultEvaluation<TModel, string>(underlyingResults, CausesInternal);

    private IEnumerable<string> ResolvedAssertions => field ??= Value switch
    {
        IEnumerable<string> reasons => reasons,
        _ => underlyingResults.GetAssertions()
    };

    /// <inheritdoc />
    public override MetadataNode<TMetadata> MetadataTier => field ??=
        new MetadataNode<TMetadata>(
            Value.ToEnumerable(),
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
        new HigherOrderExpressionTreeResultDescription<string>(
            Satisfied,
            description.ToReason(Satisfied),
            expression,
            CausesInternal,
            description.Statement);
}
