namespace Karlssberg.Motiv.Not;

internal class NotBooleanResultDescription(BooleanResultBase operand) : IResultDescription
{
    public int CausalOperandCount => 1;
    public string Reason => FormatDescription(operand.Description.Reason);
    
    public string Details => FormatDescription(operand.Description.Details);
    
    private string FormatDescription(string underlyingDescription)
    {
        return operand switch
        {
           ICompositeBooleanResult => $"({underlyingDescription})",
            _ => underlyingDescription
        };
    }
    
    public override string ToString() => Reason;
}