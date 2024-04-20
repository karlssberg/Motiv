namespace Karlssberg.Motiv.Not;

internal sealed class NotSpecDescription<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand) : ISpecDescription
{
    public string Statement => FormatDescription(operand.Description.Statement);

    public string Detailed => FormatDescription(operand.Description.Detailed);
    
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