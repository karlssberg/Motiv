namespace Karlssberg.Motiv.FirstOrder;

internal sealed class MetadataResultDescription(
    bool isSatisfied,
    IProposition proposition)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;
    
    public override string Compact => proposition.ToReason(isSatisfied);
    
    public override string Detailed => proposition.ToReason(isSatisfied);
    
    public override string ToString() => Compact;
}