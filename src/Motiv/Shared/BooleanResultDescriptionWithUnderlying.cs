namespace Motiv.Shared;

internal sealed class BooleanResultDescriptionWithUnderlying(
    BooleanResultBase booleanResult,
    string reason,
    string propositionalStatement)
    : ResultDescriptionWithUnderlying(booleanResult, reason, propositionalStatement)
{
    public override IEnumerable<string> GetJustificationAsLines()
    {
        if (IsReasonTheSameAsUnderlying())
        {
            foreach (var line in BooleanResult.Description.GetJustificationAsLines())
                yield return line;

            yield break;
        }

        yield return Reason;
        foreach (var line in BooleanResult.Description.GetJustificationAsLines())
            yield return line.Indent();
    }
}
