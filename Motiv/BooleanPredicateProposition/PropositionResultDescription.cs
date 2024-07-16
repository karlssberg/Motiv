namespace Motiv.BooleanPredicateProposition;

internal sealed class PropositionResultDescription(string reason, string propositionalStatement)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    internal override string Statement => propositionalStatement;

    public override string Justification => reason;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return reason;
    }

    public override string ToString() => Reason;
}
