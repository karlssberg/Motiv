using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.XOr;

/// <summary>
/// An asynchronous specification that represents the logical XOR of two asynchronous specifications. Both
/// operands are evaluated regardless of outcome — either sequentially (left, then right) or, when
/// <paramref name="concurrent" /> is <c>true</c>, concurrently via <see cref="Task.WhenAll(Task[])" />.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class AsyncXOrSpec<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> left,
    AsyncSpecBase<TModel, TMetadata> right,
    bool concurrent = false)
    : AsyncSpecBase<TModel, TMetadata>,
        IAsyncBinaryOperationSpec<TModel, TMetadata>,
        IAsyncFoldableOperation<TModel, TMetadata>,
        IAsyncBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    /// <inheritdoc />
    public override IEnumerable<SpecBase> Underlying => _underlying;

    /// <inheritdoc />
    public override ISpecDescription Description => field ??=
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "^", Operator.XOr,
            operand => operand is AsyncXOrSpec<TModel, TMetadata> or ExpressionXOrSpec<TModel, TMetadata>
                or XOrSpec<TModel, TMetadata>);

    /// <inheritdoc />
    public string Operation => Operator.XOr;

    /// <inheritdoc />
    public bool IsCollapsable => false;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Left => left;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Right => right;

    SpecBase IAsyncBinaryOperationSpec.Right => Right;

    SpecBase IAsyncBinaryOperationSpec.Left => Left;

    /// <summary>
    /// One entry point for both cases. The concurrent one is a fan-out rather than a walk, but the fold
    /// absorbs it into a region it starts at once rather than leaving it to evaluate itself — see
    /// <see cref="AsyncEvaluationFold" /> and
    /// <see href="https://github.com/karlssberg/Motiv/issues/145">#145</see>.
    /// </summary>
    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        AsyncEvaluationFold.MatchesAsync(this, model, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        AsyncEvaluationFold.EvaluateAsync(this, model, cancellationToken);

    AsyncSpecBase<TModel, TMetadata> IAsyncFoldableOperation<TModel, TMetadata>.FirstOperand => left;

    AsyncSpecBase<TModel, TMetadata>? IAsyncFoldableOperation<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        right;

    BooleanResultBase<TMetadata> IAsyncFoldableOperation<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        first.XOr(second!);

    bool IAsyncFoldableOperation<TModel, TMetadata>.CombineMatches(bool first, bool? second) => first ^ second!.Value;

    bool IAsyncFoldableOperation<TModel, TMetadata>.IsConcurrent => concurrent;
}
