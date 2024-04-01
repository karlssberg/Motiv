namespace Karlssberg.Motiv;

internal static class StringExtensions
{
    internal static bool IsBracketed(this string value) =>
        (value.StartsWith("(") || value.StartsWith("!(")) && value.EndsWith(")");
    
    internal static bool IsLongExpression(this string value) => value.Length > 20;
    
    internal static string EnsureBracketed(this string value) => 
        !value.IsBracketed() || value.RequiresBrackets()
            ? $"({value})"
            : value;

    internal static bool RequiresBrackets(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        
        var currentLevel = 0;
        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '!': 
                    return currentLevel <= 0;
                case '(':
                    currentLevel++;
                    break;
                case ')':
                    currentLevel--;
                    break;
                case '\\':
                    index++;
                    continue;
            }

            if (currentLevel <= 0)
                return true;
        }

        return currentLevel != 0;
    }
        
}