using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class AsyncNotPolicy<TModel, TMetadata>(
    AsyncPolicyBase<TModel, TMetadata> operand)
    : AsyncPolicyBase<TModel, TMetadata>,
        IAsyncUnaryOperationSpec,
        IAsyncOperationFold<TModel, TMetadata>
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

    AsyncSpecBase<TModel, TMetadata> IAsyncOperationFold<TModel, TMetadata>.FirstOperand => operand;

    AsyncSpecBase<TModel, TMetadata>? IAsyncOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        null;

    BooleanResultBase<TMetadata> IAsyncOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        ((PolicyResultBase<TMetadata>)first).Not();

    bool IAsyncOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => !first;

    bool IAsyncOperationFold<TModel, TMetadata>.IsConcurrent => false;

    public SpecBase Operand => operand;
}
