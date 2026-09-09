using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class AsyncNotSpec<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> operand)
    : AsyncSpecBase<TModel, TMetadata>,
        IAsyncUnaryOperationSpec,
        IAsyncFoldableOperation<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new AsyncNotSpecDescription<TModel, TMetadata>(operand);

    public string Operation => Operator.Not;

    public bool IsCollapsable => false;

    public SpecBase Operand => operand;

    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        AsyncEvaluationFold.MatchesAsync(this, model, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        AsyncEvaluationFold.EvaluateAsync(this, model, cancellationToken);

    AsyncSpecBase<TModel, TMetadata> IAsyncFoldableOperation<TModel, TMetadata>.FirstOperand => operand;

    AsyncSpecBase<TModel, TMetadata>? IAsyncFoldableOperation<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        null;

    BooleanResultBase<TMetadata> IAsyncFoldableOperation<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        first.Not();

    bool IAsyncFoldableOperation<TModel, TMetadata>.CombineMatches(bool first, bool? second) => !first;

    bool IAsyncFoldableOperation<TModel, TMetadata>.IsConcurrent => false;
}
