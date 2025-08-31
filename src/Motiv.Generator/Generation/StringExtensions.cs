using Microsoft.CodeAnalysis;

namespace Motiv.Generator.Generation;

public static class StringExtensions
{

    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new (
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public static string Capitalize(this string input) =>
        string.IsNullOrEmpty(input)
            ? input
            : $"{char.ToUpper(input[0])}{input.Substring(1)}";

    public static string ToCamelCase(this string input) =>
        string.IsNullOrEmpty(input)
            ? input
            : $"{char.ToLower(input[0])}{input.Substring(1)}";

    public static string ToParameterFieldName(this string name)
    {
        return $"_{name.ToCamelCase()}__parameter";
    }

    public static string ToIdentifier(this ITypeSymbol name)
    {
        return name
            .ToDisplayString(FullyQualifiedFormat)
            .Replace(".", "_")
            .Replace(",", "_")
            .Replace("<", "__")
            .Replace(">", "__")
            .Replace(" ", "");
    }
}
