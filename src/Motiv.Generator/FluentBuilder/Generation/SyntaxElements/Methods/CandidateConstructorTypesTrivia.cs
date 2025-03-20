using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Methods;

public static class CandidateConstructorTypesTrivia
{
    private static readonly SymbolDisplayFormat FullyQualifiedWithoutGlobal = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static SyntaxTriviaList Create(IEnumerable<IMethodSymbol> candidateConstructors)
    {
        IEnumerable<SyntaxTrivia> triviaList =
        [
            Comment($"/// <summary>"),
            CarriageReturnLineFeed,
            Comment($"/// Candidate constructor types:"),
            CarriageReturnLineFeed,
            ..candidateConstructors
                .Select(ctor => ctor.ContainingType)
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .OrderBy(type => type.ToDisplayString())
                .SelectMany<INamedTypeSymbol, SyntaxTrivia>(type =>
                [
                    CreateSeeAlsoLink(type),
                    CarriageReturnLineFeed
                ]),
            Comment($"/// </summary>"),
            CarriageReturnLineFeed,
        ];

        return TriviaList(triviaList);
    }

    private static SyntaxTrivia CreateSeeAlsoLink(INamedTypeSymbol typeSymbol)
    {
        // Get the cref format of the type symbol
        var crefValue = GetCrefAttributeValue(typeSymbol);

        // Create the <seealso> comment
        var commentText = $"///     <seealso cref=\"{crefValue}\"/>";
        return Comment(commentText);
    }

    /// <summary>
    /// Gets the formatted cref attribute value from a type symbol
    /// </summary>
    private static string GetCrefAttributeValue(INamedTypeSymbol typeSymbol)
    {
        // For non-generic types, just use the full type name
        if (!typeSymbol.IsGenericType)
        {
            return typeSymbol.ToDisplayString(FullyQualifiedWithoutGlobal);
        }

        // For generic types, we need special handling
        var baseTypeName = typeSymbol.ToDisplayString(
            new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.None));

        // Build the type arguments portion (T1, T2, etc.)
        var typeArgs = string.Join(", ", typeSymbol.TypeArguments.Select(t =>
            t.ToDisplayString(FullyQualifiedWithoutGlobal)));

        // Format as NameSpace.TypeName{T1, T2}
        return $"{baseTypeName}{{{typeArgs}}}";
    }
}
