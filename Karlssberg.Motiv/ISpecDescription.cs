namespace Karlssberg.Motiv;

public interface ISpecDescription
{
    string Statement { get; }
    string Detailed { get; }
    internal IEnumerable<string> GetDetailsAsLines();
}