using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.Propositions;

internal sealed class ResultDescription<TMetadata>(
    bool isSatisfied,
    IProposition proposition,
    TMetadata metadata)
    : IResultDescription
{
    public int CausalOperandCount => 0;
    
    public string Reason => proposition.ToReason(isSatisfied, metadata);
    
    public string Details => proposition.ToReason(isSatisfied, metadata);
    
    public override string ToString() => Reason;
}