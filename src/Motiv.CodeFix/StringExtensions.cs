namespace Motiv.CodeFix;

/// <summary>
/// Provides extension methods for strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to camel case.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The camel-cased string.</returns>
    public static string ToCamelCase(this string input) =>
        input switch
        {
            "" => input,
            _ => $"{char.ToLower(input[0])}{input.Substring(1)}"
        };

    /// <summary>
    /// Capitalizes the first letter of a string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The capitalized string.</returns>
    public static string Capitalize(this string input) =>
        input switch
        {
            "" => input,
            _ => $"{char.ToUpper(input[0])}{input.Substring(1)}"
        };

    /// <summary>
    /// Escapes double quotes in a string for use in C# string literals.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The string with escaped double quotes.</returns>
    public static string EscapeDoubleQuotes(this string input) =>
        input.Replace("\"", "\\\"");
}
