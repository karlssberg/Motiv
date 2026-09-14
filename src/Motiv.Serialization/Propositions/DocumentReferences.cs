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
        // Ordinal-ordered set: names are an ordinal contract, and callers compare graphs by content.
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (document.Root is not null)
            Collect(document.Root, names, seen);

        // Definitions are walked in their own right rather than through the locals that reference
        // them: an unreferenced definition still names specs, and a definition referenced twice must
        // not be walked twice. A local name is never an edge — it resolves inside this document, so
        // no republication anywhere can change what it means.
        foreach (var definition in document.Definitions)
            Collect(definition, names, seen);

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
