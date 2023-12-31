using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.Not;

internal sealed class NotSpecification<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> operand) : SpecificationBase<TModel, TMetadata>
{
    public SpecificationBase<TModel, TMetadata> Operand { get; } = Throw.IfNull(operand, nameof(operand));

    public override string Description => $"!({Operand})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        WrapThrownExceptions(
            this,
            Operand,
            () => Operand.IsSatisfiedBy(model).Not());
}