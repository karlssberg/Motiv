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

    /// <inheritdoc />
    public override async ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default)
    {
        if (concurrent)
        {
            var leftTask = left.MatchesAsync(model, cancellationToken).AsTask();
            var rightTask = right.MatchesAsync(model, cancellationToken).AsTask();
            await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);
            return await leftTask.ConfigureAwait(false) ^ await rightTask.ConfigureAwait(false);
        }

        return await left.MatchesAsync(model, cancellationToken).ConfigureAwait(false)
               ^ await right.MatchesAsync(model, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        if (concurrent)
        {
            var leftTask = left.EvaluateSpecAsyncInternal(model, cancellationToken).AsTask();
            var rightTask = right.EvaluateSpecAsyncInternal(model, cancellationToken).AsTask();
            await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);
            return (await leftTask.ConfigureAwait(false)).XOr(await rightTask.ConfigureAwait(false));
        }

        var leftResult = await left.EvaluateSpecAsyncInternal(model, cancellationToken).ConfigureAwait(false);
        var rightResult = await right.EvaluateSpecAsyncInternal(model, cancellationToken).ConfigureAwait(false);

        return leftResult.XOr(rightResult);
    }
}
