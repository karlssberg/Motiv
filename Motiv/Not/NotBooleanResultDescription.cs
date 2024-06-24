namespace Motiv.Not;

internal sealed class NotBooleanResultDescription(BooleanResultBase operand) : ResultDescriptionBase
{
    private readonly Dictionary<string, string> _negations = new()
    {
        ["NAND"] = "AND",
        ["NOR"] = "OR",
        ["XNOR"] = "XOR",
        ["AND"] = "NAND",
        ["OR"] = "NOR",
        ["XOR"] = "XNOR"
    };

    internal override int CausalOperandCount => 1;
    public override string Reason => operand.Description.Reason;
    public override IEnumerable<string> GetJustificationAsLines()
    {
        var lines = operand.Description
            .GetJustificationAsLines()
            .ReplaceFirstLine(firstLine =>
                _negations.TryGetValue(firstLine, out var negated)
                    ? negated
                    : firstLine);

        foreach (var line in lines)
            yield return line;
    }
}
