using Microsoft.CodeAnalysis;

namespace Motiv.Generator.Model;

public sealed class FluentParameterComparer : IEqualityComparer<IParameterSymbol>
{
    public static FluentParameterComparer Default { get; } = new();

    public bool Equals(IParameterSymbol? x, IParameterSymbol? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return x.Type.Name == y.Type.Name
               && x.Name == y.Name;
    }

    public int GetHashCode(IParameterSymbol? obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            return (obj.Type.Name.GetHashCode() * 397) ^ obj.Name.GetHashCode();
        }
    }
}
