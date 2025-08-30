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
            ..linesOfText.SelectMany(ConvertLine),
            Comment($"/// </summary>"),
            CarriageReturnLineFeed
        ];

        return TriviaList(triviaList);

        IEnumerable<SyntaxTrivia> ConvertLine(object? line)
        {
            return line switch
            {
                null => [],
                "" => [Comment("///")],
                SyntaxTrivia trivia => [trivia],
                _ when string.IsNullOrWhiteSpace(line.ToString()) => [Comment("///")],
                _ => ConvertLineEndings(line.ToString())
            };
        }

        IEnumerable<SyntaxTrivia> ConvertLineEndings(string line)
        {
            return line
                .Split(["\r\n", "\n", "\r"], default)
                .SelectMany(IEnumerable<SyntaxTrivia> (embeddedLines)  =>
                    embeddedLines switch
                    {
                        null => [],
                        "" => [Comment("///")],
                        _ => [Comment($"/// {embeddedLines}")]
                    });
        }
    }

    public static SyntaxTrivia GenerateCandidateConstructorTypePreamble(
        ICollection<IMethodSymbol> candidateConstructors)
    {
        var count = candidateConstructors
            .Select(ctor => ctor.ContainingType)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .Count();

        return Comment(count > 1
            ? "/// Candidate constructor types:"
            : "/// Constructor type:");
    }

    public static IEnumerable<SyntaxTrivia> GenerateCandidateConstructors(
        IEnumerable<IMethodSymbol> candidateConstructors)
    {
        return candidateConstructors
            .Distinct<IMethodSymbol>(SymbolEqualityComparer.Default)
            .OrderBy(type => type.Name)
            .Select(str => Comment($"///     {str.ToDisplayString()}"));;
    }

    public static IEnumerable<SyntaxTrivia> GenerateCandidateConstructorTypeSeeAlsoLinks(
        IEnumerable<IMethodSymbol> candidateConstructors)
    {
        return candidateConstructors
            .Select(ctor => ctor.ContainingType)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .OrderBy(type => type.Name)
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

    public static SyntaxTriviaList CreateWithParameters(
        IEnumerable<object?> linesOfText,
        Dictionary<string, string>? parameterDocumentation,
        IEnumerable<string> parameterNames)
    {
        var triviaList = new List<SyntaxTrivia>
        {
            Comment($"/// <summary>"),
            CarriageReturnLineFeed
        };

        triviaList.AddRange(linesOfText.SelectMany(ConvertLine));

        triviaList.Add(Comment($"/// </summary>"));
        triviaList.Add(CarriageReturnLineFeed);

        // Add parameter documentation if available
        if (parameterDocumentation != null)
        {
            foreach (var paramName in parameterNames)
            {
                if (parameterDocumentation.TryGetValue(paramName, out var paramDoc))
                {
                    triviaList.Add(Comment($"/// <param name=\"{paramName}\">{paramDoc}</param>"));
                    triviaList.Add(CarriageReturnLineFeed);
                }
            }
        }

        return TriviaList(triviaList);

        IEnumerable<SyntaxTrivia> ConvertLine(object? line)
        {
            return line switch
            {
                null => [],
                "" => [Comment("///")],
                SyntaxTrivia trivia => [trivia],
                _ when string.IsNullOrWhiteSpace(line.ToString()) => [Comment("///")],
                _ => ConvertLineEndings(line.ToString())
            };
        }

        IEnumerable<SyntaxTrivia> ConvertLineEndings(string line)
        {
            return line
                .Split(["\r\n", "\n", "\r"], default)
                .SelectMany(IEnumerable<SyntaxTrivia> (embeddedLines)  =>
                    embeddedLines switch
                    {
                        null => [],
                        "" => [Comment("///")],
                        _ => [Comment($"/// {embeddedLines}")]
                    });
        }
    }
}
