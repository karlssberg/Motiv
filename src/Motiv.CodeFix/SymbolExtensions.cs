using Microsoft.CodeAnalysis;

namespace Motiv.CodeFix;

public static class SymbolExtensions
{
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
