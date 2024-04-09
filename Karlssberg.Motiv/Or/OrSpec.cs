namespace Karlssberg.Motiv.Or;

internal sealed class OrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec
{
    public override ISpecDescription Description => 
        new OrSpecDescription<TModel, TMetadata>(left, right);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult =  left.IsSatisfiedByWithExceptionRethrowing(model);
        var rightResult = right.IsSatisfiedByWithExceptionRethrowing(model);

        return leftResult.Or(rightResult);
    }
}