using Motiv.And;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.AndAlso;

internal sealed class AsyncAndAlsoSpec<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> left,
    AsyncSpecBase<TModel, TMetadata> right)
    : AsyncSpecBase<TModel, TMetadata>,
        IAsyncBinaryOperationSpec<TModel, TMetadata>,
        IAsyncOperationFold<TModel, TMetadata>,
        IAsyncBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AsyncAndSpec<TModel, TMetadata> or AsyncAndAlsoSpec<TModel, TMetadata>
                or AsyncAndAlsoPolicy<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>
                or ExpressionAndAlsoPolicy<TModel, TMetadata>);

    public string Operation => Operator.AndAlso;

    public bool IsCollapsable => true;

    public AsyncSpecBase<TModel, TMetadata> Left => left;

    public AsyncSpecBase<TModel, TMetadata> Right => right;

    SpecBase IAsyncBinaryOperationSpec.Right => Right;

    SpecBase IAsyncBinaryOperationSpec.Left => Left;

    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        AsyncEvaluationFold.MatchesAsync(this, model, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        AsyncEvaluationFold.EvaluateAsync(this, model, cancellationToken);

    AsyncSpecBase<TModel, TMetadata> IAsyncOperationFold<TModel, TMetadata>.FirstOperand => left;

    AsyncSpecBase<TModel, TMetadata>? IAsyncOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        firstSatisfied ? right : null;

    BooleanResultBase<TMetadata> IAsyncOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        new AndAlsoBooleanResult<TMetadata>(first, second);

    bool IAsyncOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => second ?? first;

    bool IAsyncOperationFold<TModel, TMetadata>.IsConcurrent => false;
}
