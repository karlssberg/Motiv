using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.XOr;

internal sealed class XOrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "^", Operator.XOr,
            operand => operand is XOrSpec<TModel, TMetadata> or ExpressionXOrSpec<TModel, TMetadata>);

    public string Operation => Operator.XOr;
    public bool IsCollapsable => false;

    public override bool Matches(TModel model) => left.Matches(model) ^ right.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var leftResult = left.Evaluate(model);
        var rightResult = right.Evaluate(model);

        return leftResult.XOr(rightResult);
    }

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
