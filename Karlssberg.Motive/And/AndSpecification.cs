using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.And;

internal class AndSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> leftOperand,
    SpecificationBase<TModel, TMetadata> rightOperand) : SpecificationBase<TModel, TMetadata>
{
    public override string Description => $"({leftOperand}) & ({rightOperand})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = WrapException.IfIsSatisfiedByInvocationFails(
            this,
            leftOperand,
            () => leftOperand.IsSatisfiedBy(model));

        var rightResult = WrapException.IfIsSatisfiedByInvocationFails(
            this,
            rightOperand,
            () => rightOperand.IsSatisfiedBy(model));

        return leftResult.And(rightResult);
    }
}