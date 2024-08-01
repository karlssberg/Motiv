namespace Motiv.Not;

internal sealed class NotSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand)
    : SpecBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    public override IEnumerable<SpecBase> Underlying => operand.ToEnumerable();

    public override ISpecDescription Description =>
        new NotSpecDescription<TModel, TMetadata>(operand);

    public string Operation => Operator.Not;

    public bool IsCollapsable => false;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model) =>
        operand.IsSatisfiedBy(model).Not();

    public SpecBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
