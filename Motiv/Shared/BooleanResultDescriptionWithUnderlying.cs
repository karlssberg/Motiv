namespace Motiv.Shared;

internal sealed class BooleanResultDescriptionWithUnderlying(
    BooleanResultBase booleanResult,
    string reason,
    string propositionalStatement)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    internal override string Statement => propositionalStatement;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        if (IsReasonTheSameAsUnderlying())
        {
            foreach (var line in booleanResult.Description.GetJustificationAsLines())
                yield return line;

            yield break;
        }

        yield return reason;
        foreach (var line in booleanResult.Description.GetJustificationAsLines())
            yield return line.Indent();
    }

    private bool IsReasonTheSameAsUnderlying() => reason == booleanResult.Description.Reason;
}
