namespace Motiv.Or;

internal sealed class OrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{

    public override IEnumerable<SpecBase> Underlying => left.ToEnumerable().Append(right);

    public override ISpecDescription Description =>
        new OrSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => Operator.Or;

    public bool IsCollapsable => true;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var leftResult =  left.IsSatisfiedBy(model);
        var rightResult = right.IsSatisfiedBy(model);

        return leftResult.Or(rightResult);
    }

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
