namespace Karlssberg.Motiv.Not;

internal sealed class NotProposition<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand) : IProposition
{
    public string Statement => FormatDescription(operand.Proposition.Statement);

    public string Detailed => FormatDescription(operand.Proposition.Detailed);
    
    private string FormatDescription(string underlyingSummary)
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