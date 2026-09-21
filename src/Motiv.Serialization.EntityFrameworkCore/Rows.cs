using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>One row of the append-only rule version log.</summary>
/// <remarks>
/// Deliberately not <see cref="StoredRuleVersion"/> itself. Keeping the persisted shape separate
/// keeps <c>Motiv.Serialization</c> free of any EF dependency, and makes the schema an artefact this
/// package owns — so an SDK field addition breaks <see cref="RowMapping"/> at compile time rather
/// than being silently mapped by EF's conventions.
/// </remarks>
public class RuleVersionRow
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? DocumentJson { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string? ChangeNote { get; set; }
    public string? ApprovalRef { get; set; }
    public string? BuildId { get; set; }
}

/// <summary>
/// One row of the append-only proposition version log, keyed by <c>(Name, Version)</c>. A save
/// carries a document; a tombstone carries none. Kept separate from <see cref="StoredPropositionVersion"/>
/// for the reasons <see cref="RuleVersionRow"/> gives.
/// </summary>
public class PropositionVersionRow
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? ModelType { get; set; }
    public string? DocumentJson { get; set; }
    public string? Description { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string? ChangeNote { get; set; }
    public string? ApprovalRef { get; set; }
    public string? BuildId { get; set; }
}

public class StoreGenerationRow
{
    public string Scope { get; set; } = string.Empty;
    public long Generation { get; set; }
}

/// <summary>
/// Between the persisted rows and the SDK's records. Positional-record construction is the point:
/// add a parameter to <see cref="StoredRuleVersion"/> and this file stops compiling, which is the
/// loud break a schema change deserves.
/// </summary>
internal static class RowMapping
{
    public static StoredRuleVersion ToRecord(this RuleVersionRow row) =>
        new(row.Name, row.Version, row.DocumentJson, row.Author, row.TimestampUtc,
            row.ChangeNote, row.ApprovalRef, row.BuildId);

    public static RuleVersionRow ToRow(this StoredRuleVersion version) =>
        new()
        {
            Name = version.Name,
            Version = version.Version,
            DocumentJson = version.DocumentJson,
            Author = version.Author,
            TimestampUtc = version.TimestampUtc,
            ChangeNote = version.ChangeNote,
            ApprovalRef = version.ApprovalRef,
            BuildId = version.BuildId,
        };

    public static StoredPropositionVersion ToRecord(this PropositionVersionRow row) =>
        new(row.Name, row.Version, row.ModelType, row.DocumentJson, row.Description,
            row.Author, row.TimestampUtc, row.ChangeNote, row.ApprovalRef, row.BuildId);

    public static PropositionVersionRow ToRow(this StoredPropositionVersion version) =>
        new()
        {
            Name = version.Name,
            Version = version.Version,
            ModelType = version.ModelType,
            DocumentJson = version.DocumentJson,
            Description = version.Description,
            Author = version.Author,
            TimestampUtc = version.TimestampUtc,
            ChangeNote = version.ChangeNote,
            ApprovalRef = version.ApprovalRef,
            BuildId = version.BuildId,
        };
}
