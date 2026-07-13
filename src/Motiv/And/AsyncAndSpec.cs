using Motiv.AndAlso;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.And;

/// <summary>
/// An asynchronous specification that represents the logical AND of two asynchronous specifications. Both
/// operands are evaluated regardless of outcome — either sequentially (left, then right) or, when
/// <paramref name="concurrent" /> is <c>true</c>, concurrently via <see cref="Task.WhenAll(Task[])" />.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class AsyncAndSpec<TModel, TMetadata>(
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
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "&", Operator.And,
            // TODO(Task 7): add AsyncAndAlsoSpec<TModel, TMetadata> to this pattern once it exists.
            operand => operand is AsyncAndSpec<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>);

    /// <inheritdoc />
    public string Operation => Operator.And;

    /// <inheritdoc />
    public bool IsCollapsable => true;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Left => left;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Right => right;

    SpecBase IAsyncBinaryOperationSpec.Right => Right;

    SpecBase IAsyncBinaryOperationSpec.Left => Left;

    /// <inheritdoc />
    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default)
    {
        if (concurrent)
        {
            var leftTask = left.MatchesAsync(model, cancellationToken);
            var rightTask = right.MatchesAsync(model, cancellationToken);
            await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);
            return await leftTask.ConfigureAwait(false) & await rightTask.ConfigureAwait(false);
        }

        return await left.MatchesAsync(model, cancellationToken).ConfigureAwait(false)
               & await right.MatchesAsync(model, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        if (concurrent)
        {
            var leftTask = left.EvaluateAsync(model, cancellationToken);
            var rightTask = right.EvaluateAsync(model, cancellationToken);
            await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);
            return (await leftTask.ConfigureAwait(false)).And(await rightTask.ConfigureAwait(false));
        }

        var leftResult = await left.EvaluateAsync(model, cancellationToken).ConfigureAwait(false);
        var rightResult = await right.EvaluateAsync(model, cancellationToken).ConfigureAwait(false);

        return leftResult.And(rightResult);
    }
}
