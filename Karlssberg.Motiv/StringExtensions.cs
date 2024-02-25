namespace Karlssberg.Motiv;

public static class StringExtensions
{
    public static bool IsBracketed(this string value) => 
        value.StartsWith("(") && value.EndsWith(")");
    
    public static bool IsLongExpression(this string value) => value.Length > 20;
}