namespace Motiv.Tests.Traversal;

/// <summary>
/// The fixtures and shape questions the root-projection suites share. <c>RootAssertions</c> and
/// <c>RootValues</c> are twins — tickets #189 and #192 are one defect in two trees — so their suites
/// ask the same things of the same corpus and built the same leaf by hand until this existed.
/// </summary>
internal static class OracleHelpers
{
    /// <summary>An atomic proposition asserting <c>{name}-true</c> or <c>{name}-false</c>.</summary>
    internal static SpecBase<string, string> Leaf(string name, bool value) =>
        Spec.Build((string _) => value).WhenTrue($"{name}-true").WhenFalse($"{name}-false").Create();

    /// <summary>
    /// Whether a higher-order result appears anywhere in the subtree — the shape both tickets turn on,
    /// being the only node type that makes one branch deeper than its siblings.
    /// </summary>
    internal static bool ContainsHigherOrder(BooleanResultBase<string> result) =>
        result.GetType().Namespace?.StartsWith("Motiv.HigherOrderProposition", StringComparison.Ordinal) == true
        || result.UnderlyingWithValues.Any(ContainsHigherOrder);

    /// <summary>
    /// De-duplication that preserves first-seen order, written out rather than delegated to Motiv's
    /// own <c>DistinctWithOrderPreserved</c>. These suites compare a public property against an
    /// independently-computed expectation, and an expectation that borrows the production helper is
    /// one step less independent than it claims to be.
    /// </summary>
    internal static T[] DistinctInOrder<T>(IEnumerable<T> values)
    {
        var seen = new HashSet<T>();
        var ordered = new List<T>();

        foreach (var value in values)
            if (seen.Add(value))
                ordered.Add(value);

        return ordered.ToArray();
    }
}
