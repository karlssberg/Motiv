namespace Motiv.Serialization;

/// <summary>
/// Whether a name may identify a document-local definition. Deliberately not
/// <see cref="SpecRegistry.IsValidName" />: a local is not a catalog name. It never namespaces, so a
/// dot is meaningless in one, and it is a binding rather than a published proposition, so a leading
/// underscore — which the catalog grammar forbids — is the natural way to mark one as incidental.
/// </summary>
internal static class LocalNames
{
    /// <summary>
    /// The DSL's reserved words, which cannot name a local at any layer. A local is written in DSL
    /// text as well as in JSON, so a name the lexer tokenises as a keyword could never be referenced
    /// there — and a JSON document that accepted one would be unrepresentable in the surface the
    /// authoring UI edits. Mirrors <c>DSL_KEYWORDS</c>, <c>DSL_TYPES</c> and <c>DSL_QUANTIFIERS</c>
    /// in <c>ui/packages/rules-core/src/dsl/lexer.ts</c>.
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "param", "let", "in",
        "integer", "number", "string", "boolean",
        "all", "any", "exactly", "atLeast", "atMost"
    };

    /// <summary>
    /// Whether a name may be a definition key, or the target of a <c>local</c> node: an ASCII letter
    /// or <c>_</c> followed by ASCII letters, digits, <c>-</c> or <c>_</c>, and not a reserved word.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><c>true</c> when the name may be declared and referenced.</returns>
    public static bool IsValid(string name)
    {
        // A char loop rather than a Regex: this library targets netstandard2.0, and the grammar is
        // small enough that the pattern would be the harder of the two to read.
        if (string.IsNullOrEmpty(name) || Reserved.Contains(name) || !IsStart(name[0]))
            return false;

        for (var index = 1; index < name.Length; index++)
            if (!IsPart(name[index]))
                return false;

        return true;

        static bool IsStart(char character) =>
            character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';

        static bool IsPart(char character) => IsStart(character) || character is (>= '0' and <= '9') or '-';
    }
}
