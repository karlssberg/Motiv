namespace Motiv.Shared;

internal sealed class BooleanResultDescriptionWithUnderlying(
    BooleanResultBase booleanResult,
    string reason,
    string propositionalStatement)
    : ResultDescriptionWithUnderlying(booleanResult, reason, propositionalStatement)
{
    public override IEnumerable<string> GetJustificationAsLines() =>
        GetJustificationAsLinesCore(BooleanResult.Description.GetJustificationAsLines());

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        GetJustificationAsLinesCore(BooleanResult.Description.GetJustificationAsLinesWithoutCausalCount());

    private IEnumerable<string> GetJustificationAsLinesCore(IEnumerable<string> underlyingLines)
    {
        if (IsReasonTheSameAsUnderlying())
        {
            foreach (var line in underlyingLines)
                yield return line;

            yield break;
        }

        yield return Reason;
        foreach (var line in underlyingLines)
            yield return line.Indent();
    }
}
