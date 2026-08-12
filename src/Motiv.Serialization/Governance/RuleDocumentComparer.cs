namespace Motiv.Serialization;

/// <summary>
/// Structural equality of two rule documents: the logic tree without its display metadata.
/// Feeds change.is-metadata-only — a typo fix in an assertion string deserves a lighter gate
/// than a logic change.
/// </summary>
internal static class RuleDocumentComparer
{
    public static bool StructurallyEqual(RuleDocument left, RuleDocument right) =>
        NodesEqual(left.Root, right.Root) && ParametersEqual(left.Parameters, right.Parameters);

    // Recursion depth mirrors the parser's own guarded nesting depth, so parser-accepted
    // documents cannot overflow here.
    private static bool NodesEqual(RuleNode? left, RuleNode? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (left.Operator != right.Operator
            || left.SpecName != right.SpecName
            || left.ExpressionText != right.ExpressionText
            || left.N != right.N
            || left.NParameterName != right.NParameterName
            || left.PathText != right.PathText
            || left.Children.Count != right.Children.Count)
            return false;
        for (var i = 0; i < left.Children.Count; i++)
            if (!NodesEqual(left.Children[i], right.Children[i]))
                return false;
        return true;
    }

    // Parameters have no semantic order — the resolver keys them by name, and the parser rejects
    // duplicate names — so declaration order must not affect equality. Compare by name instead of
    // by index.
    private static bool ParametersEqual(
        IReadOnlyList<RuleParameterDeclaration> left,
        IReadOnlyList<RuleParameterDeclaration> right)
    {
        if (left.Count != right.Count)
            return false;
        var rightByName = right.ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var declaration in left)
        {
            if (!rightByName.TryGetValue(declaration.Name, out var match))
                return false;
            if (!ParameterEqual(declaration, match))
                return false;
        }
        return true;
    }

    private static bool ParameterEqual(RuleParameterDeclaration left, RuleParameterDeclaration right) =>
        left.Type == right.Type
        && left.HasDefault == right.HasDefault
        && Equals(left.DefaultValue, right.DefaultValue);
}
