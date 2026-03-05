using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class NotSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand)
    : SpecBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new NotSpecDescription<TModel, TMetadata>(operand);

    public string Operation => Operator.Not;

    public bool IsCollapsable => false;

    public override bool Matches(TModel model) => !operand.Matches(model);

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model) =>
        operand.IsSatisfiedBy(model).Not();

    public SpecBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
