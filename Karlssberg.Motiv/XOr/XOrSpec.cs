namespace Karlssberg.Motiv.XOr;

internal sealed class XOrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> right,
    SpecBase<TModel, TMetadata> left)
    : SpecBase<TModel, TMetadata>, ICompositeSpec
{
    public override IProposition Proposition => 
        new XOrProposition<TModel, TMetadata>(right, left);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = right.IsSatisfiedByOrWrapException(model);
        var rightResult = left.IsSatisfiedByOrWrapException(model);

        return leftResult.XOr(rightResult);
    }
}