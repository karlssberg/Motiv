using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.Not;

internal sealed class NotSpecification<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> operand) : SpecificationBase<TModel, TMetadata>
{
    public override string Description => $"!({operand})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        WrapException.IfIsSatisfiedByInvocationFails(
            this,
            operand,
            () => operand.IsSatisfiedBy(model).Not());
}