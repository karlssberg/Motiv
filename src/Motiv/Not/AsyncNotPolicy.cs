using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class AsyncNotPolicy<TModel, TMetadata>(
    AsyncPolicyBase<TModel, TMetadata> operand)
    : AsyncPolicyBase<TModel, TMetadata>,
        IAsyncUnaryOperationSpec,
        IAsyncFoldableOperation<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new AsyncNotSpecDescription<TModel, TMetadata>(operand);

    string IBooleanOperationSpec.Operation => Operator.Not;

    bool IBooleanOperationSpec.IsCollapsable => false;

    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        AsyncEvaluationFold.MatchesAsync(this, model, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        AsyncEvaluationFold.EvaluatePolicyAsync(this, model, cancellationToken);

    AsyncSpecBase<TModel, TMetadata> IAsyncFoldableOperation<TModel, TMetadata>.FirstOperand => operand;

    AsyncSpecBase<TModel, TMetadata>? IAsyncFoldableOperation<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        null;

    BooleanResultBase<TMetadata> IAsyncFoldableOperation<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        ((PolicyResultBase<TMetadata>)first).Not();

    bool IAsyncFoldableOperation<TModel, TMetadata>.CombineMatches(bool first, bool? second) => !first;

    bool IAsyncFoldableOperation<TModel, TMetadata>.IsConcurrent => false;

    public SpecBase Operand => operand;
}
