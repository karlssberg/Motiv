namespace Karlssberg.Motiv.Not;

internal class NotBooleanAssertion(BooleanResultBase operand) : IAssertion
{
    public int CausalOperandCount => 1;
    public string Short => FormatDescription(operand.Assertion.Short);
    
    public string Detailed => FormatDescription(operand.Assertion.Detailed);
    
    private string FormatDescription(string underlyingDescription)
    {
        return operand switch
        {
           ICompositeAssertion => $"({underlyingDescription})",
            _ => underlyingDescription
        };
    }
    
    public override string ToString() => Short;
}