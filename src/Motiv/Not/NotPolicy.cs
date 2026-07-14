using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class NotPolicy<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> operand)
    : PolicyBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new NotSpecDescription<TModel, TMetadata>(operand);

    string IBooleanOperationSpec.Operation => Operator.Not;

    bool IBooleanOperationSpec.IsCollapsable => false;

    public override bool Matches(TModel model) => !operand.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model) =>
        operand.EvaluatePolicyInternal(model).Not();

    public PolicyBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel, TMetadata> IUnaryOperationSpec<TModel, TMetadata>.Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
