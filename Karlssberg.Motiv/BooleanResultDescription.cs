namespace Karlssberg.Motiv;

internal sealed class BooleanResultDescription(
    string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    public override string Reason => reason;
    public override string Detailed => reason;
}