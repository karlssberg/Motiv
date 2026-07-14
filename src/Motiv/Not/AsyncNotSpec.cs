using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class AsyncNotSpec<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> operand)
    : AsyncSpecBase<TModel, TMetadata>,
        IAsyncUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new AsyncNotSpecDescription<TModel, TMetadata>(operand);

    public string Operation => Operator.Not;

    public bool IsCollapsable => false;

    public SpecBase Operand => operand;

    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        !await operand.MatchesAsync(model, cancellationToken).ConfigureAwait(false);

    protected override async Task<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        (await operand.EvaluateSpecAsyncInternal(model, cancellationToken).ConfigureAwait(false)).Not();
}
