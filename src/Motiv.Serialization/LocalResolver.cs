namespace Motiv.Serialization;

/// <summary>
/// Links every <see cref="RuleOperator.Local" /> node to the definition it names, and refuses a
/// definition graph that references itself.
/// </summary>
/// <remarks>
/// <para>
/// Runs once the whole envelope has parsed, because <c>definitions</c> may be written after the
/// <c>rule</c> that references it, and before any depth measure, because both the parser's
/// pre-filter and <see cref="CompositionDepth" /> measure a local as the definition it points at —
/// a walk that only terminates on an acyclic graph.
/// </para>
/// <para>
/// A local is linked rather than inlined: the definition node is shared by every reference to it, so
/// a binder binds one definition once per reference without the document having been rewritten into
/// a tree that no longer matches what was authored.
/// </para>
/// </remarks>
internal static class LocalResolver
{
    private enum Mark
    {
        InProgress,
        Done
    }

    /// <summary>Resolves every local reference in the document, reporting what cannot be resolved.</summary>
    /// <returns>
    /// <c>false</c> when a cycle was reported, which leaves the <see cref="RuleNode.Definition" />
    /// links unsafe to walk — no caller may measure or bind the document after that.
    /// </returns>
    public static bool Resolve(RuleDocument document, List<RuleError> errors)
    {
        var definitions = new Dictionary<string, RuleNode>(StringComparer.Ordinal);
        foreach (var definition in document.Definitions)
            definitions[definition.Name!] = definition;

        if (document.Root is not null)
            Link(document.Root, definitions, errors);
        foreach (var definition in document.Definitions)
            Link(definition, definitions, errors);

        return !ReportCycles(document.Definitions, errors);
    }

    private static void Link(RuleNode node, Dictionary<string, RuleNode> definitions, List<RuleError> errors)
    {
        if (node.Operator == RuleOperator.Local && node.LocalName is { } name)
        {
            if (definitions.TryGetValue(name, out var definition))
                node.Definition = definition;
            else
                errors.Add(new RuleError(node.Path, RuleErrorCode.UnknownLocal,
                    $"no definition named '{name}' is declared by this document"));
        }

        foreach (var child in node.Children)
            Link(child, definitions, errors);
    }

    /// <summary>
    /// Depth-first search over the definition graph, reporting each cycle once — at the first of its
    /// members reached in declaration order, which is the one the search entered the cycle through.
    /// </summary>
    /// <returns><c>true</c> when at least one cycle was reported.</returns>
    private static bool ReportCycles(IReadOnlyList<RuleNode> definitions, List<RuleError> errors)
    {
        var marks = new Dictionary<RuleNode, Mark>();
        var chain = new List<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in definitions)
            Visit(definition, marks, chain, reported, errors);

        return reported.Count > 0;
    }

    private static void Visit(
        RuleNode definition,
        Dictionary<RuleNode, Mark> marks,
        List<string> chain,
        HashSet<string> reported,
        List<RuleError> errors)
    {
        if (marks.TryGetValue(definition, out var mark))
        {
            // Still in progress means the search has arrived back where it started: a cycle. Already
            // done means a definition two others share, which is a diamond and perfectly legal.
            if (mark == Mark.InProgress)
                ReportCycle(definition.Name!, chain, reported, errors);
            return;
        }

        marks[definition] = Mark.InProgress;
        chain.Add(definition.Name!);

        foreach (var referenced in ReferencedDefinitions(definition))
            Visit(referenced, marks, chain, reported, errors);

        chain.RemoveAt(chain.Count - 1);
        marks[definition] = Mark.Done;
    }

    private static void ReportCycle(
        string name,
        List<string> chain,
        HashSet<string> reported,
        List<RuleError> errors)
    {
        if (!reported.Add(name))
            return;

        // The chain from where the cycle was entered, closed by the name it came back to, so the
        // message shows the loop rather than the path that happened to lead into it.
        var cycle = chain.Skip(chain.IndexOf(name)).Append(name);

        errors.Add(new RuleError($"$.definitions.{name}", RuleErrorCode.CycleDetected,
            $"the definition '{name}' cannot be resolved: {string.Join(" → ", cycle)} forms a reference cycle"));
    }

    /// <summary>The definitions a definition's body references, in body order.</summary>
    private static IEnumerable<RuleNode> ReferencedDefinitions(RuleNode node)
    {
        if (node.Operator == RuleOperator.Local && node.Definition is { } definition)
            yield return definition;

        foreach (var child in node.Children)
        foreach (var nested in ReferencedDefinitions(child))
            yield return nested;
    }
}
