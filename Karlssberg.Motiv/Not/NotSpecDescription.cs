using Karlssberg.Motiv.AndAlso;

namespace Karlssberg.Motiv.Not;

internal sealed class NotSpecDescription<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand) : ISpecDescription
{
    public string Statement => FormatStatement(operand.Description.Statement);

    public string Detailed => FormatStatement(operand.Description.Detailed);

    public IEnumerable<string> GetDetailsAsLines()
    {
        var lines = operand.Description
            .GetDetailsAsLines()
            .ReplaceFirstLine(firstLine => 
                firstLine.StartsWith("!")
                    ? firstLine.Substring(1)
                    : $"!{firstLine}");
        
        foreach (var line in lines)
            yield return line;
    }

    private string FormatStatement(string underlyingSummary)
    {
        return (operand, underlyingSummary.FirstOrDefault()) switch
        {
            (not IBinaryOperationSpec, '!') => underlyingSummary.Substring(1),
            (not IBinaryOperationSpec, '(') => $"!{underlyingSummary}",
            _ => $"!({underlyingSummary})"
        };
    }
    
    public override string ToString() => Statement;
}