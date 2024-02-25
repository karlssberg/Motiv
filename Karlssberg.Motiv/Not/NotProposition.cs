namespace Karlssberg.Motiv.Not;

internal class NotProposition<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand) : IProposition
{
    public string Name => FormatDescription(operand.Proposition.Name);

    public string Detailed => FormatDescription(operand.Proposition.Detailed);
    private string FormatDescription(string underlyingSummary)
    {
        return (operand, underlyingSummary.FirstOrDefault()) switch
        {
            (not ICompositeSpec, '!') => underlyingSummary.Substring(1),
            (not ICompositeSpec, '(') => $"!{underlyingSummary}",
            _ => $"!({underlyingSummary})"
        };
    }
    
    public override string ToString() => Name;
}