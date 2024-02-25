namespace Karlssberg.Motiv;

public interface IResultDescription
{
    int CausalOperandCount { get; }
    string Reason { get; }
    string Details { get; }
}