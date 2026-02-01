using Microsoft.CodeAnalysis;

namespace Motiv.CodeFix;

/// <summary>
/// Provides extension methods for <see cref="ITypeSymbol"/>.
/// </summary>
public static class SymbolExtensions
{
    /// <summary>
    /// Gets the C# type name for the specified type symbol.
    /// </summary>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <returns>The C# type name.</returns>
    public static string GetCSharpTypeName(this ITypeSymbol typeSymbol)
    {
        var typeName = typeSymbol.ToDisplayString();

        // Convert .NET type names to C# keywords
        return typeName switch
        {
            "System.Int32" => "int",
            "System.String" => "string",
            "System.Boolean" => "bool",
            "System.Double" => "double",
            "System.Single" => "float",
            "System.Int64" => "long",
            "System.Int16" => "short",
            "System.Byte" => "byte",
            "System.Decimal" => "decimal",
            _ => typeName
        };
    }
}
