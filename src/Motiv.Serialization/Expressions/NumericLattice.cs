#if NET8_0_OR_GREATER
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;

namespace Motiv.Serialization.Expressions;

/// <summary>
/// The widening rules of the leaf language: C#'s implicit numeric conversions minus the lossy
/// ones. Generic math parses literals (<c>T.Parse</c>) and <c>ConvertChecked</c>
/// performs the conversions; the *policy* — which conversions are allowed — is this table, because
/// a checked conversion refuses overflow, not precision loss.
/// </summary>
internal static class NumericLattice
{
    private static readonly (NumericKind From, NumericKind To)[] Widenings =
    [
        (NumericKind.Int32, NumericKind.Int64),
        (NumericKind.Int32, NumericKind.Decimal),
        (NumericKind.Int64, NumericKind.Decimal),
        (NumericKind.Int32, NumericKind.Double),
        (NumericKind.Single, NumericKind.Double),
    ];

    public static NumericKind? KindOf(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(int)) return NumericKind.Int32;
        if (underlying == typeof(long)) return NumericKind.Int64;
        if (underlying == typeof(float)) return NumericKind.Single;
        if (underlying == typeof(double)) return NumericKind.Double;
        if (underlying == typeof(decimal)) return NumericKind.Decimal;
        return null;
    }

    public static Type ClrType(NumericKind kind) => kind switch
    {
        NumericKind.Int32 => typeof(int),
        NumericKind.Int64 => typeof(long),
        NumericKind.Single => typeof(float),
        NumericKind.Double => typeof(double),
        _ => typeof(decimal),
    };

    public static bool IsIntegral(NumericKind kind) => kind is NumericKind.Int32 or NumericKind.Int64;

    public static bool CanWiden(NumericKind from, NumericKind to) =>
        from == to || Array.IndexOf(Widenings, (from, to)) >= 0;

    public static NumericKind? Join(NumericKind a, NumericKind b)
    {
        if (CanWiden(a, b)) return b;
        if (CanWiden(b, a)) return a;
        return null;
    }

    public static Expression Constant(string text, NumericKind kind) => kind switch
    {
        NumericKind.Int32 => Parse<int>(text, NumberStyles.Integer),
        NumericKind.Int64 => Parse<long>(text, NumberStyles.Integer),
        NumericKind.Single => Parse<float>(text, NumberStyles.Number),
        NumericKind.Double => Parse<double>(text, NumberStyles.Number),
        _ => Parse<decimal>(text, NumberStyles.Number),
    };

    private static ConstantExpression Parse<T>(string text, NumberStyles style) where T : INumber<T> =>
        Expression.Constant(T.Parse(text, style, CultureInfo.InvariantCulture), typeof(T));

    public static Expression Widen(Expression value, NumericKind to)
    {
        var target = ClrType(to);
        if (Nullable.GetUnderlyingType(value.Type) is not null)
            target = typeof(Nullable<>).MakeGenericType(target);
        return value.Type == target ? value : Expression.ConvertChecked(value, target);
    }
}
#endif
