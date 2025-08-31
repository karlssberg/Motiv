using Microsoft.CodeAnalysis;

namespace Motiv.Generator.Model;

public class FluentType(ITypeSymbol typeSymbol) : IEquatable<FluentType>
{
    private readonly string _key = typeSymbol.ToDisplayString();

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
