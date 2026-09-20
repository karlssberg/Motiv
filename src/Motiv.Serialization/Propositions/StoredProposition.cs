namespace Motiv.Serialization;

/// <summary>The head of an authored proposition as a store reports it: the live row, never a tombstone.</summary>
public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);

/// <summary>Where a store stands for one name: its highest version, and whether that row is live.</summary>
/// <param name="Version">The highest version the log holds for the name, tombstones included.</param>
/// <param name="Live">False when the highest row is a tombstone.</param>
public sealed record PropositionPosition(int Version, bool Live);

/// <summary>
/// One row of a proposition's version log. A save writes a row with a document; a deletion writes a
/// <em>tombstone</em> — a row one past the deleted version with no document — so the log records that
/// the name was withdrawn, by whom, and when, and a later re-creation continues the numbering rather
/// than reusing a version the decision log may already pin.
/// </summary>
/// <param name="ModelType">The model type of the saved document, carried onto the tombstone from the row it retires.</param>
/// <param name="DocumentJson">The document, or null for a tombstone.</param>
public sealed record StoredPropositionVersion(
    string Name,
    int Version,
    string? ModelType,
    string? DocumentJson,
    string? Description,
    string Author,
    DateTimeOffset TimestampUtc,
    string? ChangeNote,
    string? ApprovalRef,
    string? BuildId)
{
    /// <summary>True when this row records a withdrawal rather than a document.</summary>
    public bool IsTombstone => DocumentJson is null;

    /// <summary>The row a save writes.</summary>
    public static StoredPropositionVersion Saved(
        StoredProposition proposition, RuleChangeProvenance provenance, DateTimeOffset timestampUtc)
    {
        var stamped = provenance.WithDefaults();
        return new StoredPropositionVersion(
            proposition.Name, proposition.Version, proposition.ModelType, proposition.DocumentJson,
            proposition.Description, stamped.Author, timestampUtc,
            stamped.ChangeNote, stamped.ApprovalRef, stamped.BuildId);
    }

    /// <summary>The row a deletion writes: one past the deleted version, with no document.</summary>
    public static StoredPropositionVersion Tombstone(
        PropositionDeletion deletion, string? modelType, RuleChangeProvenance provenance,
        DateTimeOffset timestampUtc)
    {
        var stamped = provenance.WithDefaults();
        return new StoredPropositionVersion(
            deletion.Name, deletion.Version + 1, modelType, DocumentJson: null, Description: null,
            stamped.Author, timestampUtc, stamped.ChangeNote, stamped.ApprovalRef, stamped.BuildId);
    }

    /// <summary>The position of a name given its rows, or null when there are none.</summary>
    public static PropositionPosition? PositionOf(IEnumerable<StoredPropositionVersion> rows)
    {
        var highest = Highest(rows);
        return highest is null ? null : new PropositionPosition(highest.Version, !highest.IsTombstone);
    }

    /// <summary>The live head given a name's rows, or null when there are none or the head is a tombstone.</summary>
    public static StoredProposition? HeadOf(IEnumerable<StoredPropositionVersion> rows)
    {
        var highest = Highest(rows);
        return highest is null || highest.IsTombstone
            ? null
            : new StoredProposition(highest.Name, highest.ModelType!, highest.DocumentJson!, highest.Version, highest.Description);
    }

    /// <summary>
    /// The live heads of a whole log, grouped by name — every name whose highest row carries a
    /// document. What an in-memory or file-backed store's <c>Load</c> answers with.
    /// </summary>
    public static IReadOnlyList<StoredProposition> HeadsOf(
        IEnumerable<IEnumerable<StoredPropositionVersion>> logsByName)
    {
        var heads = new List<StoredProposition>();
        foreach (var rows in logsByName)
        {
            if (HeadOf(rows) is { } head)
                heads.Add(head);
        }

        return heads;
    }

    private static StoredPropositionVersion? Highest(IEnumerable<StoredPropositionVersion> rows)
    {
        StoredPropositionVersion? highest = null;
        foreach (var row in rows)
        {
            if (highest is null || row.Version > highest.Version)
                highest = row;
        }

        return highest;
    }
}
