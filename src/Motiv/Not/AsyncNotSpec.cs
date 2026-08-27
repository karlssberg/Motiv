using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class AsyncNotSpec<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> operand)
    : AsyncSpecBase<TModel, TMetadata>,
        IAsyncUnaryOperationSpec,
        IAsyncOperationFold<TModel, TMetadata>
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

    AsyncSpecBase<TModel, TMetadata> IAsyncOperationFold<TModel, TMetadata>.FirstOperand => operand;

    AsyncSpecBase<TModel, TMetadata>? IAsyncOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        null;

    BooleanResultBase<TMetadata> IAsyncOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        first.Not();

    bool IAsyncOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => !first;

    bool IAsyncOperationFold<TModel, TMetadata>.IsConcurrent => false;
}
