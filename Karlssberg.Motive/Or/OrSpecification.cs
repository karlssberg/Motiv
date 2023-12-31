using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.Or;

internal sealed class OrSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> leftOperand,
    SpecificationBase<TModel, TMetadata> rightOperand)
    : SpecificationBase<TModel, TMetadata>
{
    public SpecificationBase<TModel, TMetadata> LeftOperand { get; } = Throw.IfNull(leftOperand, nameof(leftOperand));
    public SpecificationBase<TModel, TMetadata> RightOperand { get; } = Throw.IfNull(rightOperand, nameof(rightOperand));

    public override string Description => $"({LeftOperand}) OR ({RightOperand})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = WrapThrownExceptions(
            this,
            LeftOperand,
            () => LeftOperand.IsSatisfiedBy(model));

        var rightResult = WrapThrownExceptions(
            this,
            RightOperand,
            () => RightOperand.IsSatisfiedBy(model));

        return leftResult.Or(rightResult);
    }
}