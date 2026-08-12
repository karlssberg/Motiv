namespace Motiv.Serialization;

/// <summary>
/// Dot-boundary namespace-prefix matching, shared by the grant evaluator and the
/// <c>change.in-namespace</c> gate spec so "covers" means one thing everywhere.
/// </summary>
public static class NamespacePrefix
{
    /// <summary>
    /// Whether <paramref name="prefix"/> covers <paramref name="name"/>: the empty prefix covers
    /// everything; otherwise the prefix must equal the name or end on a whole dotted segment of it.
    /// </summary>
    public static bool Covers(string prefix, string name)
    {
        if (prefix.Length == 0)
            return true;
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        return name.Length == prefix.Length || name[prefix.Length] == '.';
    }
}
