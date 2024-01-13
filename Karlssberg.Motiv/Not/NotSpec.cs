using static Karlssberg.Motiv.SpecException;

namespace Karlssberg.Motiv.Not;

internal sealed class NotSpec<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand) : SpecBase<TModel, TMetadata>
{
    public override string Description => $"!({operand})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        WrapException.IfIsSatisfiedByInvocationFails(
            this,
            operand,
            () => operand.IsSatisfiedBy(model).Not());
}