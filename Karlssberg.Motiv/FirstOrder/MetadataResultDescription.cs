namespace Karlssberg.Motiv.FirstOrder;

internal sealed class MetadataResultDescription(string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    public override string Reason => reason;

    public override string Detailed => reason;
    
    public override string ToString() => Reason;
}