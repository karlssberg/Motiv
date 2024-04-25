namespace Karlssberg.Motiv;

public abstract class ResultDescriptionBase
{
    internal abstract int CausalOperandCount { get; }
    
    public abstract string Reason { get; }
    
    public virtual string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());
    
    public override string ToString() => Reason;

    public abstract IEnumerable<string> GetDetailsAsLines();
}