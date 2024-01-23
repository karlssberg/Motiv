namespace Karlssberg.Motiv.Or;

internal sealed class OrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> leftOperand,
    SpecBase<TModel, TMetadata> rightOperand)
    : SpecBase<TModel, TMetadata>
{
    public override string Description => $"({leftOperand}) | ({rightOperand})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult =  leftOperand.IsSatisfiedByOrWrapException(model);
        var rightResult = rightOperand.IsSatisfiedByOrWrapException(model);

        return leftResult.Or(rightResult);
    }
}