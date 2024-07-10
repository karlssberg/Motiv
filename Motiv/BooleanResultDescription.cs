namespace Motiv;

internal sealed class BooleanResultDescription(
    string reason,
    IEnumerable<string>? subReasons = null)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return reason;
        foreach (var line in subReasons ?? [])
            yield return line.Indent();
    }
}
