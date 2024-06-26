namespace Motiv.Not;

internal sealed class NotSpecDescription<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand) : ISpecDescription
{
    private readonly Dictionary<string, string> _negations = new()
    {
        ["NAND"] = "AND",
        ["NAND ALSO"] = "NAND ALSO",
        ["NOR"] = "OR",
        ["NOR ELSE"] = "OR ELSE",
        ["XNOR"] = "XOR",
        ["AND"] = "NAND",
        ["AND ALSO"] = "NAND ALSO",
        ["OR"] = "NOR",
        ["OR ELSE"] = "NOR ELSE",
        ["XOR"] = "XNOR"
    };

    public string Statement => FormatStatement();

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        var lines = operand.Description
            .GetDetailsAsLines()
            .ReplaceFirstLine(firstLine =>
            {
                if (_negations.TryGetValue(firstLine, out var negated))
                    return negated;

                return firstLine.StartsWith("!")
                    ? firstLine.Substring(1)
                    : $"!{firstLine}";
            });

        foreach (var line in lines)
            yield return line;
    }

    private string FormatStatement()
    {
        var firstChar = operand.Statement.FirstOrDefault();
        return firstChar switch
        {
            '!' => operand.Statement.Substring(1),
            not '!' when ContainsBinaryOperation(operand) =>  $"!({operand.Statement})",
            _ =>  $"!{operand.Statement}"
        };
    }

    private static bool ContainsBinaryOperation(SpecBase spec) =>
        spec switch
        {
            IBinaryOperationSpec => true,
            NotSpec<TModel, TMetadata> => false,
            _ => spec.Underlying.Any(ContainsBinaryOperation)
        };

    public override string ToString() => Statement;
}
