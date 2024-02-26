namespace Karlssberg.Motiv;

public interface IAssertion
{
    internal int CausalOperandCount { get; }
    string Short { get; }
    string Detailed { get; }
}