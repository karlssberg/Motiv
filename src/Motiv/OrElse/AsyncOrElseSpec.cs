using Motiv.Or;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.OrElse;

/// <summary>
/// An asynchronous specification that represents the conditional OR of two asynchronous specifications. The
/// right operand is only evaluated if the left operand resolves to <c>false</c> — for asynchronous
/// specifications this means the right operand's work (including any I/O) is never started when the left
/// operand is satisfied.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class AsyncOrElseSpec<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> left,
    AsyncSpecBase<TModel, TMetadata> right)
    : AsyncSpecBase<TModel, TMetadata>,
        IAsyncBinaryOperationSpec<TModel, TMetadata>,
        IAsyncBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    /// <inheritdoc />
    public override IEnumerable<SpecBase> Underlying => _underlying;

    /// <inheritdoc />
    public override ISpecDescription Description => field ??=
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "||", Operator.OrElse,
            operand => operand is AsyncOrSpec<TModel, TMetadata> or AsyncOrElseSpec<TModel, TMetadata>
                or AsyncOrElsePolicy<TModel, TMetadata>
                or OrSpec<TModel, TMetadata> or OrElseSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata>
                or ExpressionOrSpec<TModel, TMetadata> or ExpressionOrElseSpec<TModel, TMetadata>
                or ExpressionOrElsePolicy<TModel, TMetadata>);

    /// <inheritdoc />
    public string Operation => Operator.OrElse;

    /// <inheritdoc />
    public bool IsCollapsable => true;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Left => left;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Right => right;

    SpecBase IAsyncBinaryOperationSpec.Right => Right;

    SpecBase IAsyncBinaryOperationSpec.Left => Left;

    /// <inheritdoc />
    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        await left.MatchesAsync(model, cancellationToken).ConfigureAwait(false)
        || await right.MatchesAsync(model, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async Task<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var leftResult = await left.EvaluateSpecAsyncInternal(model, cancellationToken).ConfigureAwait(false);
        return leftResult.Satisfied switch
        {
            true => new OrElseBooleanResult<TMetadata>(leftResult),
            false => new OrElseBooleanResult<TMetadata>(
                leftResult,
                await right.EvaluateSpecAsyncInternal(model, cancellationToken).ConfigureAwait(false))
        };
    }
}
