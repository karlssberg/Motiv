namespace Motiv.Serialization.Sql;

/// <summary>
/// One bounded page of the decision log — the question "why was <em>this</em> customer declined, on
/// the 3rd, at 14:07?" written down.
/// </summary>
/// <remarks>
/// Deliberately small. Reading lives on <see cref="SqlDecisionSink"/> rather than on
/// <c>IDecisionSink</c>, because that seam is also "emit, don't store" and a sink forwarding to a SIEM
/// has nothing to read back — putting a query on the interface would make every such implementation
/// lie. The same reasoning caps what belongs here: enough to answer the question the log exists for,
/// and not so much that it becomes a reporting API nobody decided to build.
/// </remarks>
public sealed record DecisionQuery
{
    private readonly int _limit = 100;

    /// <summary>
    /// The decision to pivot on. Several rules evaluated inside one decision share a correlation id,
    /// so this is what turns one record into the whole decision it belonged to.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>One rule's evaluations, or null for every rule.</summary>
    public string? RuleName { get; init; }

    /// <summary>
    /// Only satisfied or only unsatisfied evaluations, or null for both. "Show me the declines" is
    /// the question this exists for, and it is a predicate the database applies rather than a scan
    /// through serialised justification trees.
    /// </summary>
    public bool? Satisfied { get; init; }

    /// <summary>The inclusive start of the window, or null for unbounded.</summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>The inclusive end of the window, or null for unbounded.</summary>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>The most records to return, newest first. Defaults to 100.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int Limit
    {
        get => _limit;
        init => _limit = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Limit must be at least 1.");
    }
}
