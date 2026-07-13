using Motiv.And;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.AndAlso;

internal sealed class AsyncAndAlsoSpec<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> left,
    AsyncSpecBase<TModel, TMetadata> right)
    : AsyncSpecBase<TModel, TMetadata>,
        IAsyncBinaryOperationSpec<TModel, TMetadata>,
        IAsyncBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AsyncAndSpec<TModel, TMetadata> or AsyncAndAlsoSpec<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>);

    public string Operation => Operator.AndAlso;

    public bool IsCollapsable => true;

    public AsyncSpecBase<TModel, TMetadata> Left => left;

    public AsyncSpecBase<TModel, TMetadata> Right => right;

    SpecBase IAsyncBinaryOperationSpec.Right => Right;

    SpecBase IAsyncBinaryOperationSpec.Left => Left;

    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        await left.MatchesAsync(model, cancellationToken).ConfigureAwait(false)
        && await right.MatchesAsync(model, cancellationToken).ConfigureAwait(false);

    protected override async Task<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var leftResult = await left.EvaluateSpecAsyncInternal(model, cancellationToken).ConfigureAwait(false);
        return leftResult.Satisfied switch
        {
            true => new AndAlsoBooleanResult<TMetadata>(
                leftResult,
                await right.EvaluateSpecAsyncInternal(model, cancellationToken).ConfigureAwait(false)),
            false => new AndAlsoBooleanResult<TMetadata>(leftResult)
        };
    }
}
