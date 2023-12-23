using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.Or;

public sealed class OrSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> leftOperand,
    SpecificationBase<TModel, TMetadata> rightOperand)
    : SpecificationBase<TModel, TMetadata>
{
    public SpecificationBase<TModel, TMetadata> LeftOperand { get; } = Throw.IfNull(leftOperand, nameof(leftOperand));
    public SpecificationBase<TModel, TMetadata> RightOperand { get; } = Throw.IfNull(rightOperand, nameof(rightOperand));

    public override BooleanResultBase<TMetadata> Evaluate(TModel model)
    {
        var leftResult = WrapThrownExceptions(
            this,
            LeftOperand,
            () => LeftOperand.Evaluate(model));
            
        var rightResult = WrapThrownExceptions(
            this,
            RightOperand,
            () => RightOperand.Evaluate(model));
        
        return leftResult.Or(rightResult);
    }

    public override string Description => $"({LeftOperand}) OR ({RightOperand})";
}