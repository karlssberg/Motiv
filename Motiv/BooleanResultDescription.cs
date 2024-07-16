namespace Motiv;

internal sealed class BooleanResultDescription(
    string reason,
    string propositionalStatement,
    IEnumerable<string>? subReasons = null)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    internal override string Statement => propositionalStatement;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return reason;
        foreach (var line in subReasons ?? [])
            yield return line.Indent();
    }
}
