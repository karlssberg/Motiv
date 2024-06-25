namespace Motiv.Not;

internal sealed class NotBooleanResultDescription<TMetadata>(BooleanResultBase operand) : ResultDescriptionBase
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

    public override string Reason => FormatReason(operand);

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

    private string FormatReason(BooleanResultBase result)
    {
        return result switch
        {
            NotBooleanResult<TMetadata> notResult => NegateNotOperator(notResult),
            IBooleanOperationResult =>  $"!({result.Reason})",
            _ =>$"{result.Reason}"
        };
    }

    private static string NegateNotOperator(NotBooleanResult<TMetadata> notResult)
    {
        var count = 0;
        var current = notResult;
        while (current.Operand is NotBooleanResult<TMetadata> nested)
        {
            count++;
            current = nested;
        }



        return (count % 2 == 0, current.Operand) switch
        {
            (true, _) => current.Operand.Reason,
            (false, IBooleanOperationResult) => $"!({current.Operand.Reason})",
            (false, _) => $"!{current.Operand.Reason}",
        };
    }
}
