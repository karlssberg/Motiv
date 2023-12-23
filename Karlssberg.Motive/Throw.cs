using System.Runtime.CompilerServices;

namespace Karlssberg.Motive;

public static class Throw
{
    public static T IfNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName, "value cannot be null");
        
        return value;
    }
    
    public static string IfNulOrEmpty(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("string value cannot be null or empty", paramName);
        
        return value;
    }
    
    public static string IfNullOrWhitespace(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("string value cannot be null, empty or whitespace", paramName);
        
        return value;
    }
}