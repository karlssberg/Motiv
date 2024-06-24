namespace Motiv;

internal sealed class BooleanResultDescription(
    string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    public override string Reason => reason;
    
    public override IEnumerable<string> GetJustificationAsLines() => reason.ToEnumerable();
}