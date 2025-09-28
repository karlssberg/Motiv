namespace Motiv.CodeFix;

public static class StringExtensions
{
    public static string ToCamelCase(this string input) =>
        input switch
        {
            "" => input,
            _ => $"{char.ToLower(input[0])}{input.Substring(1)}"
        };

    public static string Capitalize(this string input) =>
        input switch
        {
            "" => input,
            _ => $"{char.ToUpper(input[0])}{input.Substring(1)}"
        };
}
