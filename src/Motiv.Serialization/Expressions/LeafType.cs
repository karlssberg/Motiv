#if NET8_0_OR_GREATER
namespace Motiv.Serialization.Expressions;

/// <summary>A numeric type still being solved. Union-find: <see cref="Parent" /> chains to the
/// representative, which alone carries the resolution.</summary>
internal sealed class TypeVar
{
    public TypeVar? Parent;
    public bool Fractional;
    public RuleParameterType? ParamKind;
    public NumericKind? Resolved;
    public string? ResolvedBy;
    public readonly List<LeafNode> Members = [];

    public TypeVar Root => Parent is null ? this : Parent.Root;

    public bool Allows(NumericKind kind)
    {
        if (Fractional && NumericLattice.IsIntegral(kind)) return false;
        return ParamKind switch
        {
            RuleParameterType.Integer => kind is NumericKind.Int32 or NumericKind.Int64 or NumericKind.Decimal,
            RuleParameterType.Number => kind is NumericKind.Decimal or NumericKind.Double or NumericKind.Single,
            _ => true,
        };
    }
}

/// <summary>
/// The type of a node during checking: a concrete CLR type, a type variable, or unknown (already
/// reported). <paramref name="NamedEnum" /> carries the enum behind a member that reads as a
/// string, so equality can check a literal against its member names.
/// </summary>
internal readonly record struct LeafType(Type? Concrete, TypeVar? Var, Type? NamedEnum = null)
{
    public static readonly LeafType Unknown = new(null, null);
    public static LeafType Of(Type type) => new(type, null);
    public static LeafType Of(Type type, Type? namedEnum) => new(type, null, namedEnum);
    public static LeafType OfVar(TypeVar var) => new(null, var);
    public bool IsUnknown => Concrete is null && Var is null;
    public bool IsNullable => Concrete is not null && (!Concrete.IsValueType || Nullable.GetUnderlyingType(Concrete) is not null);
    public Type? Underlying => Concrete is null ? null : Nullable.GetUnderlyingType(Concrete) ?? Concrete;
}
#endif
