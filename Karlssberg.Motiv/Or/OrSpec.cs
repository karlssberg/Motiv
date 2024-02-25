namespace Karlssberg.Motiv.Or;

internal sealed class OrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, ICompositeSpec
{
    public override IProposition Proposition => 
        new OrProposition<TModel, TMetadata>(left, right);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult =  left.IsSatisfiedByOrWrapException(model);
        var rightResult = right.IsSatisfiedByOrWrapException(model);

        return leftResult.Or(rightResult);
    }
}