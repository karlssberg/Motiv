namespace Karlssberg.Motiv;

internal static class Throw
{
    internal static T ThrowIfNull<T>(this T? value, string paramName)
    {
        if (value is null)
            throw new ArgumentNullException(paramName, $"'{paramName}' cannot be null");

        return value;
    }

    internal static string ThrowIfNulOrEmpty(this string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"the string '{paramName}' cannot be null or empty", paramName);

        return value;
    }

    internal static string ThrowIfNullOrWhitespace(this string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"the string '{paramName}' cannot be null, empty or whitespace", paramName);

        return value;
    }

    internal static T ThrowIfFactoryOutputIsNull<T>(this T? value, string factoryName)
    {
        if (value is null)
            throw new ArgumentNullException(factoryName, $"The output of the factory '{factoryName}' cannot be null");

        return value;
    }

    internal static T ThrowIfGreaterThan<T>(this T value, T maximum, string paramName) where T : IComparable<T>
    {
        if (value.CompareTo(maximum) > 0)
            throw new ArgumentOutOfRangeException($"the parameter '{paramName}' must be less than or equal to {maximum}.");

        return value;
    }
    
    internal static T ThrowIfLessThan<T>(this T value, T minimum, string paramName) where T : IComparable<T>
    {
        if (value.CompareTo(minimum) < 0)
            throw new ArgumentOutOfRangeException($"the parameter '{paramName}' must be greater than or equal {minimum}.");

        return value;
    }
}