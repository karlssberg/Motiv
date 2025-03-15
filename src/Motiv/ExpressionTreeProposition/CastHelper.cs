namespace Motiv.ExpressionTreeProposition;

internal static class CastHelper
{
    internal static bool IsExplicitNumericCast(Type sourceType, Type targetType)
    {
        sourceType = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // If types are the same, no cast is needed
        if (sourceType == targetType) return false;


        // Handle nullable types
        var underlyingSourceType = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var underlyingTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;


        // Numeric type conversions
        if (IsNumericType(underlyingSourceType) && IsNumericType(underlyingTargetType))
        {
            return !IsImplicitNumericConversion(underlyingSourceType, underlyingTargetType);
        }


        // By default, assume implicit cast since
        return false;
    }
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(long) ||
               type == typeof(ulong) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(decimal);
    }

    private static bool IsImplicitNumericConversion(Type source, Type target)
    {
        // Based on C# language specification for implicit numeric conversions
        if (source == typeof(sbyte))
            return target == typeof(short) ||
                   target == typeof(int) ||
                   target == typeof(long) ||
                   target == typeof(float) ||
                   target == typeof(double) ||
                   target == typeof(decimal);

        if (source == typeof(byte))
            return target == typeof(short) ||
                   target == typeof(ushort) ||
                   target == typeof(int) ||
                   target == typeof(uint) ||
                   target == typeof(long) ||
                   target == typeof(ulong) ||
                   target == typeof(float) ||
                   target == typeof(double) ||
                   target == typeof(decimal);

        if (source == typeof(short))
            return target == typeof(int) ||
                   target == typeof(long) ||
                   target == typeof(float) ||
                   target == typeof(double) ||
                   target == typeof(decimal);

        if (source == typeof(ushort))
            return target == typeof(int) ||
                   target == typeof(uint) ||
                   target == typeof(long) ||
                   target == typeof(ulong) ||
                   target == typeof(float) ||
                   target == typeof(double) ||
                   target == typeof(decimal);

        if (source == typeof(int))
            return target == typeof(long) ||
                   target == typeof(float) ||
                   target == typeof(double) ||
                   target == typeof(decimal);

        if (source == typeof(uint))
            return target == typeof(long) ||
                   target == typeof(ulong) ||
                   target == typeof(float) ||
                   target == typeof(double) ||
                   target == typeof(decimal);

        if (source == typeof(long) || source == typeof(ulong))
            return target == typeof(float) ||
                   target == typeof(double) ||
                   target == typeof(decimal);

        if (source == typeof(float))
            return target == typeof(double);

        if (source == typeof(decimal))
            return false; // decimal has no implicit conversions to other types

        return false;
    }
}
