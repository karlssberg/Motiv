using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class NotSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand)
    : SpecBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IOperationFold<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new NotSpecDescription<TModel, TMetadata>(operand);

    public string Operation => Operator.Not;

    public bool IsCollapsable => false;

    public override bool Matches(TModel model) => EvaluationFold.Matches(this, model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) =>
        EvaluationFold.Evaluate(this, model);

    SpecBase<TModel, TMetadata> IOperationFold<TModel, TMetadata>.FirstOperand => operand;

    SpecBase<TModel, TMetadata>? IOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) => null;

    BooleanResultBase<TMetadata> IOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        first.Not();

    bool IOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => !first;

    public SpecBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
