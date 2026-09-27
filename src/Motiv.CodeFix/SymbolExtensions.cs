using Microsoft.CodeAnalysis;

namespace Motiv.CodeFix;

/// <summary>
/// Provides extension methods for <see cref="ISymbol"/> and <see cref="ITypeSymbol"/>.
/// </summary>
public static class SymbolExtensions
{
    /// <summary>
    /// Extracts the <see cref="ITypeSymbol"/> from a variable symbol
    /// (parameter, local, field, or property).
    /// </summary>
    public static ITypeSymbol? GetTypeSymbol(this ISymbol symbol) =>
        symbol switch
        {
            IParameterSymbol parameter => parameter.Type,
            ILocalSymbol local => local.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };
}
