using static Karlssberg.Motiv.SpecificationException;

namespace Karlssberg.Motiv.Not;

internal sealed class NotSpecification<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> operand) : SpecificationBase<TModel, TMetadata>
{
    public override string Description => $"!({operand})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        WrapException.IfIsSatisfiedByInvocationFails(
            this,
            operand,
            () => operand.IsSatisfiedBy(model).Not());
}