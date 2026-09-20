namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Which document a rule ran at a version, answered the same way by every surface that prints or
/// serves one. The live version is the bound entry's; any other is the version log's row; and
/// version 1 — the default, bound at startup and never published, so the log holds no row for it —
/// is the default's own document, which a compiled default does not have.
/// </summary>
internal static class RuleVersionLookup
{
    /// <summary>
    /// The document at <paramref name="version"/>, or the live one when it is null. <c>Exists</c>
    /// is false when the rule never had that version; <c>DocumentJson</c> is null when it did but
    /// ran compiled code.
    /// </summary>
    public static async Task<(bool Exists, string? DocumentJson)> DocumentAtAsync(
        RuleBase rule, RuleSetEntry live, IRuleStore? store, int? version, CancellationToken cancellationToken)
    {
        if (version is null || version == live.Version)
            return (true, live.DocumentJson);

        if (store is not null
            && (await store.HistoryAsync(rule.Name, cancellationToken)).FirstOrDefault(row => row.Version == version) is { } row)
            return (true, row.DocumentJson);

        return version == 1 ? (true, rule.Default.DocumentJson) : (false, null);
    }
}
