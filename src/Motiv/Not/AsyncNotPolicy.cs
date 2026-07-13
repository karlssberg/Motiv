using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class AsyncNotPolicy<TModel, TMetadata>(
    AsyncPolicyBase<TModel, TMetadata> operand)
    : AsyncPolicyBase<TModel, TMetadata>,
        IAsyncUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new AsyncNotSpecDescription<TModel, TMetadata>(operand);

    string IBooleanOperationSpec.Operation => Operator.Not;

    bool IBooleanOperationSpec.IsCollapsable => false;

    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        !await operand.MatchesAsync(model, cancellationToken).ConfigureAwait(false);

    protected override async Task<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        (await operand.EvaluateAsync(model, cancellationToken).ConfigureAwait(false)).Not();

    public SpecBase Operand => operand;
}
