using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.Propositions;

internal sealed class ResultDescription<TMetadata>(
    bool isSatisfied,
    IProposition proposition,
    TMetadata metadata)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;
    
    public override string Compact => proposition.ToReason(isSatisfied, metadata);
    
    public override string Detailed => proposition.ToReason(isSatisfied, metadata);
    
    public override string ToString() => Compact;
}