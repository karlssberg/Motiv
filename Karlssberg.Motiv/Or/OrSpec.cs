using static Karlssberg.Motiv.SpecException;

namespace Karlssberg.Motiv.Or;

internal sealed class OrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> leftOperand,
    SpecBase<TModel, TMetadata> rightOperand)
    : SpecBase<TModel, TMetadata>
{
    public override string Description => $"({leftOperand}) | ({rightOperand})";

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

        return leftResult.Or(rightResult);
    }
}