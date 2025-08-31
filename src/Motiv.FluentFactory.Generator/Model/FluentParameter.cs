using Microsoft.CodeAnalysis;

namespace Motiv.FluentFactory.Generator.Model;


public class FluentMethodParameter(
    IParameterSymbol parameterSymbol,
    IEnumerable<string> names)
    : IEquatable<FluentMethodParameter>
{
    public FluentMethodParameter(
        IParameterSymbol parameterSymbol,
        string names)
        : this(parameterSymbol, [names])
    {
    }

    public IParameterSymbol ParameterSymbol { get; } = parameterSymbol;

    public FluentType FluentType { get; } = new(parameterSymbol.Type);

    public ISet<string> Names { get; } = new HashSet<string>(names);

    public bool Equals(FluentMethodParameter? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!FluentType.Equals(other.FluentType)) return false;
        return Names.Overlaps(other.Names);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((FluentMethodParameter)obj);
    }

    public override int GetHashCode() => FluentType.GetHashCode();

    public override string ToString()
    {
        var names = Names.Select(name => $"'{name}'");
        return $"{FluentType} ({string.Join(", ", names)})";
    }
}
