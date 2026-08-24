using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderResultDescription<TUnderlyingMetadata>(
    string reason,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : HigherOrderResultDescriptionBase<TUnderlyingMetadata>(reason, causes, propositionStatement)
{
    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        FoldedJustification(withoutCausalCount: true);

    private protected override string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        Render(withoutCausalCount
            ? UnderlyingJustifications(operandLines)
            : UnderlyingJustificationsWithCounts(operandLines))
            .ToArray();

    private IEnumerable<string> Render(IEnumerable<string> underlyingLines)
    {
        yield return Reason;

        foreach (var line in underlyingLines)
        {
            yield return line.Indent();
        }
    }
}
