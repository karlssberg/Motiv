namespace Karlssberg.Motiv;

public record Reason(string Description, IEnumerable<Reason>? UnderlyingReasons = null)
{
    public IEnumerable<Reason> UnderlyingReasons { get; } = UnderlyingReasons ?? Enumerable.Empty<Reason>();
    
    public string Description { get; } = Description;
    
    public override string ToString() => Description;
}