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
        var leftResult = right.IsSatisfiedByWithExceptionRethrowing(model);
        var rightResult = left.IsSatisfiedByWithExceptionRethrowing(model);

        return leftResult.XOr(rightResult);
    }
}