﻿namespace Karlssberg.Motiv;

public static class Throw
{
    public static T ThrowIfNull<T>(this T? value, string paramName)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName, $"'{paramName}' cannot be null");

        return value;
    }

    public static string ThrowIfNulOrEmpty(this string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"the string '{paramName}' cannot be null or empty", paramName);

        return value;
    }

    public static string ThrowIfNullOrWhitespace(this string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"the string '{paramName}' cannot be null, empty or whitespace", paramName);

        return value;
    }

    public static T ThrowIfFactoryOutputIsNull<T>(this T? value, string factoryName)
    {
        if (value is null)
            throw new ArgumentNullException(factoryName, $"The output of the factory '{factoryName}' cannot be null");

        return value;
    }
}