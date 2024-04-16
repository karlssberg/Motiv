namespace Karlssberg.Motiv.XOr;

internal sealed class XOrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> right,
    SpecBase<TModel, TMetadata> left)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec
{
    public override ISpecDescription Description => 
        new XOrSpecDescription<TModel, TMetadata>(right, left);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = right.IsSatisfiedBy(model);
        var rightResult = left.IsSatisfiedBy(model);

        return leftResult.XOr(rightResult);
    }
}