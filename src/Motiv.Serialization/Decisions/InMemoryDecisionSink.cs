namespace Motiv.Serialization;

/// <summary>
/// A decision sink that keeps everything in memory. The reference implementation and the default for
/// development, tests and the sample app — never for production, where the log must outlive the
/// process and be bounded by a retention window. It keeps what it is given, so it is also the
/// reference <see cref="IDecisionSource"/>.
/// </summary>
public sealed class InMemoryDecisionSink : IDecisionSink, IDecisionSource
{
    private readonly List<DecisionRecord> _records = [];
    private readonly List<DecisionGap> _gaps = [];
    private readonly object _lock = new();

    /// <summary>Every record written, oldest first.</summary>
    public IReadOnlyList<DecisionRecord> Records
    {
        get { lock (_lock) return [.. _records]; }
    }

    /// <summary>Every gap marker written, oldest first. A gap is evidence about the log, not a decision.</summary>
    public IReadOnlyList<DecisionGap> Gaps
    {
        get { lock (_lock) return [.. _gaps]; }
    }

    /// <inheritdoc />
    public Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken)
    {
        lock (_lock) _records.AddRange(records);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken)
    {
        lock (_lock) _gaps.Add(gap);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DecisionRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<DecisionRecord?>(Records.FirstOrDefault(record => record.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<DecisionRecord>> QueryAsync(DecisionQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<DecisionRecord> rows = Records;
        if (query.CorrelationId is { } correlation) rows = rows.Where(r => r.CorrelationId == correlation);
        if (query.RuleName is { } rule) rows = rows.Where(r => r.RuleName == rule);
        if (query.Satisfied is { } satisfied) rows = rows.Where(r => r.Outcome.Satisfied == satisfied);
        if (query.FromUtc is { } from) rows = rows.Where(r => r.TimestampUtc >= from);
        if (query.ToUtc is { } to) rows = rows.Where(r => r.TimestampUtc <= to);
        return Task.FromResult<IReadOnlyList<DecisionRecord>>(
            [.. rows.OrderByDescending(r => r.TimestampUtc).Take(query.Limit)]);
    }
}
