namespace Motiv.Serialization;

/// <summary>
/// Structural equality of two rule documents: the logic tree without its display metadata.
/// Feeds change.is-metadata-only — a typo fix in an assertion string deserves a lighter gate
/// than a logic change.
/// </summary>
internal static class RuleDocumentComparer
{
    // Audited is compared alongside the tree, not with the display text it sits beside. It decides
    // whether every evaluation of this rule leaves evidence, so a change to it is a change to what the
    // rule does -- and letting it travel under the metadata-only ceremony would mean the audit trail
    // could be switched off with the gate reserved for typo fixes.
    public static bool StructurallyEqual(RuleDocument left, RuleDocument right) =>
        left.Audited == right.Audited
        && NodesEqual(left.Root, right.Root)
        && ParametersEqual(left.Parameters, right.Parameters);

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
            || !ArgumentsEqual(left.Args, right.Args)
            || left.Children.Count != right.Children.Count)
            return false;
        for (var i = 0; i < left.Children.Count; i++)
            if (!NodesEqual(left.Children[i], right.Children[i]))
                return false;
        return true;
    }

    // An argument feeds the spec a parameterised entry builds, so it is logic, not display text: a
    // changed threshold is a changed rule. Like parameter declarations, arguments are name-keyed
    // and have no semantic order, so JSON property order must not affect equality.
    private static bool ArgumentsEqual(
        IReadOnlyDictionary<string, object?>? left,
        IReadOnlyDictionary<string, object?>? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (left.Count != right.Count)
            return false;
        return left.All(argument =>
            right.TryGetValue(argument.Key, out var match) && Equals(argument.Value, match));
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
        return left.All(declaration =>
            rightByName.TryGetValue(declaration.Name, out var match)
            && ParameterEqual(declaration, match));
    }

    private static bool ParameterEqual(RuleParameterDeclaration left, RuleParameterDeclaration right) =>
        left.Type == right.Type
        && left.HasDefault == right.HasDefault
        && Equals(left.DefaultValue, right.DefaultValue);
}
