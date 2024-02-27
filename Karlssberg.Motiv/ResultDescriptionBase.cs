namespace Karlssberg.Motiv;

public abstract class ResultDescriptionBase
{
    internal abstract int CausalOperandCount { get; }
    public abstract string Compact { get; }
    public abstract string Detailed { get; }
    public override string ToString() => Compact;
}