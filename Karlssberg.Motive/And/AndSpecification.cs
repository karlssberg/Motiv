using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.And;

public sealed class AndSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> leftOperand,
    SpecificationBase<TModel, TMetadata> rightOperand) : SpecificationBase<TModel, TMetadata>
{
    public SpecificationBase<TModel, TMetadata> LeftOperand { get; } = Throw.IfNull(leftOperand, nameof(leftOperand));
    public SpecificationBase<TModel, TMetadata> RightOperand { get; } = Throw.IfNull(rightOperand, nameof(rightOperand));

    public override string Description => $"({LeftOperand}) AND ({RightOperand})";

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
        
        return leftResult.And(rightResult);
    }
}