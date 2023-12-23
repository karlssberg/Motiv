using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.Not;

public sealed class NotSpecification<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> operand) : SpecificationBase<TModel, TMetadata>
{
    public SpecificationBase<TModel, TMetadata> Operand { get; } = Throw.IfNull(operand, nameof(operand));

    public override BooleanResultBase<TMetadata> Evaluate(TModel model) => 
        WrapThrownExceptions(
            this,
            Operand,
            () => Operand.Evaluate(model).Not());

    public override string Description => $"!({Operand})";
}