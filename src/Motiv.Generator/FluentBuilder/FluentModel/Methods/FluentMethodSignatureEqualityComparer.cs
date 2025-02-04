namespace Motiv.Generator.FluentBuilder.FluentModel.Methods;

internal sealed class FluentMethodSignatureEqualityComparer : IEqualityComparer<IFluentMethod>
{
    public static FluentMethodSignatureEqualityComparer Default { get; } = new();

    public bool Equals(IFluentMethod? x, IFluentMethod? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;
        if (x.MethodName != y.MethodName) return false;

        if (!x.MethodParameters
                .Select(p => p.FluentType)
                .SequenceEqual(y.MethodParameters.Select(p => p.FluentType)))
            return false;

        return x.TypeParameters.SequenceEqual(y.TypeParameters);
    }

    public int GetHashCode(IFluentMethod obj)
    {
        var hash = 397 ^ obj.MethodName.GetHashCode();

        hash = obj.MethodParameters.Aggregate(hash, (accumulator, parameter) =>
            accumulator * 397 ^ parameter.FluentType.GetHashCode());

        hash = obj.TypeParameters.Aggregate(hash, (accumulator, typeParameter) =>
            accumulator * 397 ^ typeParameter.GetHashCode());

        return hash;
    }
}
