namespace Karlssberg.Motiv.Not;

internal class NotBooleanResultDescription(BooleanResultBase operand) : IResultDescription
{
    public string Reason => FormatDescription(operand.Description.Reason);
    
    public string Details => FormatDescription(operand.Description.Details);
    
    private string FormatDescription(string underlyingDescription)
    {
        return (underlyingDescription.FirstOrDefault(), operand) switch
        {
            (_, ICompositeBooleanResult) => $"!({underlyingDescription})",
            ('!', _) => underlyingDescription.Substring(1),
            _ => $"!{underlyingDescription}"
        };
    }
    
    public override string ToString() => Reason;
}