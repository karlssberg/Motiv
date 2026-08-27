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
        IAsyncOperationFold<TModel, TMetadata>,
        IAsyncBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    /// <inheritdoc />
    public override IEnumerable<SpecBase> Underlying => _underlying;

    /// <inheritdoc />
    public override ISpecDescription Description => field ??=
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "&", Operator.And,
            operand => operand is AsyncAndSpec<TModel, TMetadata> or AsyncAndAlsoSpec<TModel, TMetadata>
                or AsyncAndAlsoPolicy<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>
                or ExpressionAndAlsoPolicy<TModel, TMetadata>);

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

    /// <summary>
    /// The concurrent case is a fan-out rather than a walk, so it stays as it was and the fold leaves it
    /// to evaluate itself. The sequential case — the only one a rule document can produce — is folded.
    /// </summary>
    public override async ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default)
    {
        if (!concurrent)
            return await AsyncEvaluationFold.MatchesAsync(this, model, cancellationToken).ConfigureAwait(false);

        var leftTask = left.MatchesAsync(model, cancellationToken).AsTask();
        var rightTask = right.MatchesAsync(model, cancellationToken).AsTask();
        await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);
        return await leftTask.ConfigureAwait(false) & await rightTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        if (!concurrent)
            return await AsyncEvaluationFold.EvaluateAsync(this, model, cancellationToken).ConfigureAwait(false);

        var leftTask = left.EvaluateSpecAsyncInternal(model, cancellationToken).AsTask();
        var rightTask = right.EvaluateSpecAsyncInternal(model, cancellationToken).AsTask();
        await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);
        return (await leftTask.ConfigureAwait(false)).And(await rightTask.ConfigureAwait(false));
    }

    AsyncSpecBase<TModel, TMetadata> IAsyncOperationFold<TModel, TMetadata>.FirstOperand => left;

    AsyncSpecBase<TModel, TMetadata>? IAsyncOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        right;

    BooleanResultBase<TMetadata> IAsyncOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        first.And(second!);

    bool IAsyncOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => first & second!.Value;

    bool IAsyncOperationFold<TModel, TMetadata>.IsConcurrent => concurrent;
}
