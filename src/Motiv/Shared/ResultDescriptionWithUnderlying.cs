namespace Motiv.Shared;

internal abstract class ResultDescriptionWithUnderlying(
    BooleanResultBase booleanResult,
    string reason,
    string propositionalStatement)
    : ResultDescriptionBase
{
    protected BooleanResultBase BooleanResult => booleanResult;

    internal override int CausalOperandCount => 1;

    internal override string Statement => propositionalStatement;

    public override string Reason => reason;

    protected bool IsReasonTheSameAsUnderlying() => reason == booleanResult.Description.Reason;
}
