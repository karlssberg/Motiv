using Microsoft.CodeAnalysis;

namespace Motiv.Generator.FluentFactory;

public static class StringExtensions
{
    public static string ToFileName(this INamedTypeSymbol namedTypeSymbol)
    {
        return namedTypeSymbol
            .ToDisplayString()
            .Replace("<", "__")
            .Replace(">", "__")
            .Replace(',', '_');
    }
}
