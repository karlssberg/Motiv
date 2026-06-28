using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderResultDescription<TUnderlyingMetadata>(
    string reason,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : HigherOrderResultDescriptionBase<TUnderlyingMetadata>(reason, causes, propositionStatement)
{
    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return Reason;

        foreach (var line in GetUnderlyingJustificationsWithCountsAsLines())
        {
            yield return line.Indent();
        }
    }

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount()
    {
        yield return Reason;

        foreach (var line in GetUnderlyingJustificationsAsLines())
        {
            yield return line.Indent();
        }
    }
}
