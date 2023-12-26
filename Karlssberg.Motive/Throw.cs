namespace Karlssberg.Motive;

public static class Throw
{
    public static T IfNull<T>(T? value, string paramName)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName, $"'{paramName}' cannot be null");

        return value;
    }

    public static string IfNulOrEmpty(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"the string '{paramName}' cannot be null or empty", paramName);

        return value;
    }

    public static string IfNullOrWhitespace(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"the string '{paramName}' cannot be null, empty or whitespace", paramName);

        return value;
    }

    public static T IfFactoryOutputIsNull<T>(T? value, string factoryName)
    {
        if (value is null)
            throw new ArgumentNullException(factoryName, $"The output of the factory '{factoryName}' cannot be null");

        return value;
    }
}