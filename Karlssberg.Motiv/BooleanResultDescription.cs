namespace Karlssberg.Motiv;

internal sealed class BooleanResultDescription(
    string because)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 0;

    public override string Compact => because;
    public override string Detailed => because;
}