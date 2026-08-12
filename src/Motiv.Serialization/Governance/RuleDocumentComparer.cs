namespace Motiv.Serialization;

/// <summary>
/// Structural equality of two rule documents: the logic tree without its display metadata.
/// Feeds change.is-metadata-only — a typo fix in an assertion string deserves a lighter gate
/// than a logic change.
/// </summary>
internal static class RuleDocumentComparer
{
    public static bool StructurallyEqual(RuleDocument left, RuleDocument right) =>
        NodesEqual(left.Root, right.Root);

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
}
