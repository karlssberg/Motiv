using Microsoft.CodeAnalysis;

namespace Motiv.Generator.FluentBuilder.Model;

public class FluentTypeParameter(ITypeParameterSymbol typeParameterSymbol) : IEquatable<FluentTypeParameter>
{
    private readonly string _key = typeParameterSymbol.ToDisplayString();

    public ITypeParameterSymbol TypeParameterSymbol { get; } = typeParameterSymbol;

    public bool Equals(FluentTypeParameter? other)
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
        return Equals((FluentTypeParameter)obj);
    }

    public override int GetHashCode() => _key.GetHashCode();

    public override string ToString() => _key;
}
