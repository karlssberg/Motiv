namespace Karlssberg.Motiv.Not;

internal sealed class NotBooleanResultDescription(BooleanResultBase operand) : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Compact => FormatDescription(operand.Description.Compact);
    
    public override string Detailed => FormatDescription(operand.Description.Detailed);
    
    private string FormatDescription(string underlyingDescription)
    {
        return operand switch
        {
           ICompositeBooleanResult => $"({underlyingDescription})",
            _ => underlyingDescription
        };
    }
}