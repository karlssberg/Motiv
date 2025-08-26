using Microsoft.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Generation.SyntaxElements.Methods;

public static class FluentMethodSummaryDocXml
{
    private static readonly SymbolDisplayFormat FullyQualifiedWithoutGlobal = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static SyntaxTriviaList Create(
        IEnumerable<object?> linesOfText)
    {
        IEnumerable<SyntaxTrivia> triviaList =
        [
            Comment($"/// <summary>"),
            CarriageReturnLineFeed,
            ..linesOfText.SelectMany(line  =>
                line switch
                {
                    null or "" => [],
                    SyntaxTrivia trivia => [trivia],
                    _ => string.IsNullOrWhiteSpace(line.ToString())
                        ? []
                        : line.ToString()
                            .Split(["\r\n", "\n", "\r"], default)
                            .Select(embeddedLines => string.IsNullOrEmpty(embeddedLines)
                                ? Comment("///")
                                : Comment($"/// {embeddedLines}"))
                }),
            Comment($"/// </summary>"),
            CarriageReturnLineFeed
        ];

        return TriviaList(triviaList);
    }

    public static SyntaxTrivia GenerateCandidateConstructorPreamble(
        ICollection<IMethodSymbol> candidateConstructors)
    {
        return Comment(candidateConstructors.Count > 1
            ? "/// Candidate constructor types:"
            : "/// Constructor type:");
    }

    public static IEnumerable<SyntaxTrivia> GenerateCandidateConstructorSeeAlsoLinks(
        IEnumerable<IMethodSymbol> candidateConstructors)
    {
        return candidateConstructors
            .Select(ctor => ctor.ContainingType)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .OrderBy(type => type.ToDisplayString())
            .Select(CreateSeeAlsoLink);
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
