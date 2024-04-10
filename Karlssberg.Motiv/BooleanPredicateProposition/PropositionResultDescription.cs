namespace Karlssberg.Motiv.BasicProposition;

internal sealed class PropositionResultDescription(string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    public override string Reason => reason;

    public override string Detailed => reason;
    
    public override string ToString() => Reason;
}