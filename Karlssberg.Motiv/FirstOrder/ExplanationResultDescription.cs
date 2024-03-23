namespace Karlssberg.Motiv.FirstOrder;

internal sealed class ExplanationResultDescription(
    string because)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;
    
    public override string Reason => because;
    
    public override string Detailed => because;
    
    public override string ToString() => Reason;
}