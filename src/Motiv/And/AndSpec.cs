using Motiv.AndAlso;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.And;

internal sealed class AndSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "&", Operator.And,
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>);

    public string Operation => Operator.And;

    public bool IsCollapsable => true;

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;

    public override bool Matches(TModel model) => left.Matches(model) & right.Matches(model);

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var leftResult = left.IsSatisfiedBy(model);
        var rightResult = right.IsSatisfiedBy(model);

        return leftResult.And(rightResult);
    }
}
