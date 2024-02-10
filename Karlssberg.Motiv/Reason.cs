namespace Karlssberg.Motiv;

public record Reason(string Value, IEnumerable<Reason>? UnderlyingCauses = null)
{
    public IEnumerable<Reason> UnderlyingCauses { get; } = UnderlyingCauses ?? Enumerable.Empty<Reason>();
    
    public string Value { get; } = Value;
    
    public override string ToString() => Value;
}