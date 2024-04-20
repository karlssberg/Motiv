namespace Karlssberg.Motiv.Not;

internal sealed class NotBooleanResultDescription<TMetadata>(BooleanResultBase<TMetadata> operand) : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Reason => FormatDescription(operand.Description.Reason);
    
    public override string Detailed => FormatDescription(operand.Description.Detailed);
    
    private string FormatDescription(string underlyingDescription)
    {
        return operand switch
        {
           IBinaryBooleanOperationResult<TMetadata> => $"({underlyingDescription})",
            _ => underlyingDescription
        };
    }
}