namespace Motiv.Shared;

internal sealed class BooleanResultDescriptionWithUnderlying(
    BooleanResultBase booleanResult,
    string reason,
    string propositionalStatement)
    : ResultDescriptionWithUnderlying(booleanResult, reason, propositionalStatement)
{
    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        FoldedJustification(withoutCausalCount: true);

    private protected override IReadOnlyList<Rendering> JustificationOperands(bool withoutCausalCount) =>
        [new Rendering(BooleanResult.Description, withoutCausalCount)];

    private protected override string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        GetJustificationAsLinesCore(operandLines[0]).ToArray();

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
