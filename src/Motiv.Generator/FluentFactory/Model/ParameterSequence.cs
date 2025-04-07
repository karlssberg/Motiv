using System.Collections;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Motiv.Generator.FluentFactory.Model;

public class ParameterSequence : IEquatable<ParameterSequence>, IEnumerable<IParameterSymbol>
{
    private ImmutableArray<IParameterSymbol> ParameterSymbols { get; }
    private (string Type, string Name)[] ParameterSymbolsPrecomputed { get; }

    private readonly int _hashCode;

    public ParameterSequence()
    {
        ParameterSymbols = [];
        ParameterSymbolsPrecomputed = [];
        _hashCode = 101;
    }

    public ParameterSequence(IEnumerable<IParameterSymbol> parameterSymbols)
    {
        ImmutableArray<IParameterSymbol> parameterSymbolsArray = [..parameterSymbols];
        ParameterSymbols = parameterSymbolsArray;
        ParameterSymbolsPrecomputed = parameterSymbolsArray.Select(p => (Type: p.Type.ToDisplayString(), Name: p.GetFluentMethodName())).ToArray();
        _hashCode = parameterSymbolsArray
            .Select(p => (p.Type, Name: p.GetFluentMethodName()))
            .Aggregate(
                101, (left, right) =>
                    left * 397 ^ right.Type.ToDisplayString().GetHashCode() * 397 ^ right.Name.GetHashCode());
    }

    public ParameterSequence(IEnumerable<FluentMethodParameter> fluentParameters) : this(fluentParameters.Select(fp => fp.ParameterSymbol))
    {
    }

    public IEnumerator<IParameterSymbol> GetEnumerator()
    {
        return ParameterSymbols.AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)ParameterSymbols).GetEnumerator();
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals(obj as ParameterSequence);
    }

    public bool Equals(ParameterSequence? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ParameterSymbolsPrecomputed.SequenceEqual(other.ParameterSymbolsPrecomputed);
    }

    public static implicit operator ImmutableArray<IParameterSymbol> (ParameterSequence knownConstructorParameters)
    {
        return knownConstructorParameters.ParameterSymbols;
    }

    public static bool operator ==(ParameterSequence left, ParameterSequence right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ParameterSequence left, ParameterSequence right)
    {
        return !(left == right);
    }
}
