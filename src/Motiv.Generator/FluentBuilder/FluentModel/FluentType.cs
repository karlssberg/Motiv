using Microsoft.CodeAnalysis;

namespace Motiv.Generator.FluentBuilder.FluentModel;

public class FluentType(ITypeSymbol typeSymbol, string? methodName = null) : IEquatable<FluentType>
{
    private readonly string _key = typeSymbol.ToDisplayString();

    public ITypeSymbol TypeSymbol { get; } = typeSymbol;

    public bool Equals(FluentType? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _key.Equals(other._key);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((FluentType)obj);
    }

    public override int GetHashCode() => _key.GetHashCode();

    public override string ToString() => _key;
}
