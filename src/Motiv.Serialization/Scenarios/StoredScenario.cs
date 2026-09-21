namespace Motiv.Serialization;

/// <summary>
/// One named sample model attached to a rule — a row of the rule's scenario table. Test data, not
/// behaviour: it never changes what a rule decides, so it is neither versioned as a log nor governed.
/// </summary>
/// <param name="RuleName">The rule the scenario belongs to.</param>
/// <param name="Id">Stable within the rule; chosen by the caller so a client can create without a round trip.</param>
/// <param name="Name">The row's label.</param>
/// <param name="ModelJson">The model as typed — kept verbatim, JSON or not, so an edit in progress survives.</param>
/// <param name="ExpectedSatisfied">Null for a sample to look at; set for a test to hold.</param>
/// <param name="SourceDecisionId">The logged decision this scenario was saved from, when it was.</param>
/// <param name="Version">The compare-and-set token: a put at 0 creates, otherwise it must equal the stored one.</param>
/// <param name="Author">Who last wrote it.</param>
/// <param name="TimestampUtc">When it was last written; stamped by the store.</param>
public sealed record StoredScenario(
    string RuleName,
    string Id,
    string Name,
    string ModelJson,
    bool? ExpectedSatisfied,
    string? SourceDecisionId,
    int Version,
    string Author,
    DateTimeOffset TimestampUtc);

/// <summary>The outcome of a scenario write: landed at a version, or refused as stale.</summary>
public sealed class ScenarioWriteResult
{
    private ScenarioWriteResult(bool isConflict, int version, int currentVersion)
    {
        IsConflict = isConflict;
        Version = version;
        CurrentVersion = currentVersion;
    }

    /// <summary>Whether the write was refused because the base version was not the stored one.</summary>
    public bool IsConflict { get; }

    /// <summary>The version the row is at after a successful write.</summary>
    public int Version { get; }

    /// <summary>The version the row is actually at when refused, or 0 when the store holds no such row.</summary>
    public int CurrentVersion { get; }

    /// <summary>The write landed and the row is now at <paramref name="version"/>.</summary>
    public static ScenarioWriteResult Written(int version) => new(false, version, version);

    /// <summary>Nothing landed: the row is actually at <paramref name="currentVersion"/>.</summary>
    public static ScenarioWriteResult Conflict(int currentVersion) => new(true, 0, currentVersion);
}
