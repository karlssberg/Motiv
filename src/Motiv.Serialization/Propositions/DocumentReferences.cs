namespace Motiv.Serialization;

/// <summary>
/// Reads the spec names a document references. These are the outgoing edges of the dependency
/// graph: the set of propositions whose republication changes this document's meaning.
/// </summary>
internal static class DocumentReferences
{
    /// <summary>The distinct spec names the document references, in document order.</summary>
    public static IReadOnlyList<string> From(RuleDocument document)
    {
        if (document.Root is null)
            return [];

        // Ordinal-ordered set: names are an ordinal contract, and callers compare graphs by content.
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Collect(document.Root, names, seen);
        return names;
    }

    private static void Collect(RuleNode node, List<string> names, HashSet<string> seen)
    {
        if (node.Operator == RuleOperator.Spec && node.SpecName is { } name && seen.Add(name))
            names.Add(name);

        foreach (var child in node.Children)
            Collect(child, names, seen);
    }
}
