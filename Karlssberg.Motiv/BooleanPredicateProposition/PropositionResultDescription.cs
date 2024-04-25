namespace Karlssberg.Motiv.BooleanPredicateProposition;

internal sealed class PropositionResultDescription(string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;
    
    public override string Detailed => reason;

    public override string Reason => reason;

    public override IEnumerable<string> GetDetailsAsLines()
    {
        yield return reason;
    }

    public override string ToString() => Reason;
}