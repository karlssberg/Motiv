using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.Propositions;

internal sealed class Assertion<TMetadata>(
    bool isSatisfied,
    IProposition proposition,
    TMetadata metadata)
    : IAssertion
{
    public int CausalOperandCount => 0;
    
    public string Short => proposition.ToReason(isSatisfied, metadata);
    
    public string Detailed => proposition.ToReason(isSatisfied, metadata);
    
    public override string ToString() => Short;
}