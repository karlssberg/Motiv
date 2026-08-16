namespace Motiv.Serialization;

/// <summary>
/// Where published rules are kept between restarts — the rule-side twin of
/// <see cref="IPropositionStore"/>. The two are symmetrical and are <em>never written in the same
/// transaction</em>: they coordinate independently, and no <em>write</em> spans both — not even inside
/// a governed envelope that publishes a rule and a proposition together, which persists the rule half
/// and the proposition half as two separate store calls.
/// </summary>
/// <remarks>
/// <para>
/// A store is a dumb sink for <em>semantic</em> legality — it validates no document and enforces no
/// rule-level invariant; <see cref="RuleSet"/> decides all of that before anything reaches here. It is
/// not, however, dumb about <em>structure</em>: the <c>(Name, Version)</c> primary key is load-bearing.
/// It is the compare-and-set that makes a lost update impossible across processes, and
/// <see cref="AppendAsync"/> reporting a conflict is how a stale writer finds out.
/// </para>
/// <para>
/// The log is append-only and kept forever. A rollback does not rewrite history — restoring v5 appends
/// v9 carrying v5's document. <see cref="RuleSet.RestoreAsync"/> defaults the written row's change note
/// to name the version it restored from whenever the caller supplies none, so the appended row is
/// itself readable evidence that a rollback happened, not merely a document that happens to match one.
/// </para>
/// <para>
/// <see cref="LoadAsync"/> and <see cref="GetGenerationAsync"/> back <see cref="RuleSet.RefreshAsync"/>:
/// a replica polls <see cref="GetGenerationAsync"/> on a timer (see
/// <c>Motiv.Serialization.AspNetCore.MotivRefreshService</c>, an opt-in background poller) and only
/// calls <see cref="LoadAsync"/> — the expensive rebuild path — once that scalar has actually moved.
/// <see cref="HistoryAsync"/> is exercised by <see cref="RuleSet.RestoreAsync"/>.
/// </para>
/// </remarks>
public interface IRuleStore
{
    /// <summary>
    /// Every rule's head, read once at startup. Synchronous because startup is: the DI factory wall
    /// cannot await, and paying for an async path there would buy nothing.
    /// </summary>
    IReadOnlyList<StoredRule> Load();

    /// <summary>
    /// Every rule's head, read on a refresh. Separate from <see cref="Load"/> rather than replacing it
    /// because the two run at different times under different constraints. Called by
    /// <see cref="RuleSet.RefreshAsync"/>, and only once <see cref="GetGenerationAsync"/> has shown the
    /// store moved — see the interface remarks.
    /// </summary>
    Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A monotonically increasing number that moves whenever a write lands, so a replica can tell
    /// whether it is behind without re-reading anything.
    /// </summary>
    /// <remarks>
    /// <strong>Must be a scalar read.</strong> An implementation that answers this by loading the
    /// store defeats the entire point — it is polled on a timer by every replica, via
    /// <see cref="RuleSet.RefreshAsync"/> and, opt-in, <c>Motiv.Serialization.AspNetCore.MotivRefreshService</c>.
    /// It must also never move backwards while replicas are live, including across a restore: it is
    /// the fencing token behind monotonic-read consistency.
    /// </remarks>
    Task<long> GetGenerationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Appends version rows — all of them, or none. A row whose <c>(Name, Version)</c> already exists
    /// is refused, and the whole batch with it.
    /// </summary>
    /// <remarks>
    /// The batch is not a convenience. A governed publish validates a whole envelope, then persists it,
    /// then mutates memory; a per-row call would put a failure point after mutation had begun and break
    /// "a failed persist leaves nothing live".
    /// </remarks>
    Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken);

    /// <summary>Every recorded version of one rule, oldest first. Empty when the name is unknown.</summary>
    Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken cancellationToken);
}

/// <summary>
/// The outcome of an <see cref="IRuleStore.AppendAsync"/>. A conflict is an expected outcome — a
/// second writer arriving with the same version — so it is a value, not an exception.
/// </summary>
public sealed class RuleAppendResult
{
    private RuleAppendResult(bool isConflict, string? name, int currentVersion)
    {
        IsConflict = isConflict;
        Name = name;
        CurrentVersion = currentVersion;
    }

    /// <summary>Whether the batch was refused because a row's version was already taken.</summary>
    public bool IsConflict { get; }

    /// <summary>The rule whose version was taken, or null when nothing conflicted.</summary>
    public string? Name { get; }

    /// <summary>The version that name is actually at, or 0 when nothing conflicted.</summary>
    public int CurrentVersion { get; }

    /// <summary>Every row landed.</summary>
    public static RuleAppendResult Appended { get; } = new(false, null, 0);

    /// <summary>Nothing landed: <paramref name="name"/> is already at <paramref name="currentVersion"/>.</summary>
    public static RuleAppendResult Conflict(string name, int currentVersion) =>
        new(true, name, currentVersion);
}

/// <summary>The default store: rules live for the lifetime of the process, as they always have.</summary>
/// <remarks>
/// Real, not a stub — it implements the same primary key, so the conflict path this store produces is
/// the one a database store produces, and a test written against it holds against Postgres.
/// </remarks>
public sealed class InMemoryRuleStore : IRuleStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<StoredRuleVersion>> _log = new(StringComparer.Ordinal);
    private long _generation;

    /// <inheritdoc />
    public IReadOnlyList<StoredRule> Load()
    {
        lock (_gate)
            return [.. _log.Values.Select(Head)];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    /// <inheritdoc />
    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult(_generation);
    }

    /// <inheritdoc />
    public Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            // Check every row before writing any of them: the batch is all-or-nothing, and there is
            // no rollback here — refusing up front is what makes that true.
            foreach (var version in versions)
            {
                if (_log.TryGetValue(version.Name, out var existing)
                    && existing.Any(row => row.Version == version.Version))
                {
                    return Task.FromResult(
                        RuleAppendResult.Conflict(version.Name, existing.Max(row => row.Version)));
                }
            }

            foreach (var version in versions)
            {
                if (!_log.TryGetValue(version.Name, out var rows))
                    _log[version.Name] = rows = [];
                rows.Add(version);
            }

            if (versions.Count > 0)
                _generation++;

            return Task.FromResult(RuleAppendResult.Appended);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredRuleVersion>>(
                _log.TryGetValue(name, out var rows)
                    ? [.. rows.OrderBy(row => row.Version)]
                    : []);
        }
    }

    /// <summary>The head projection: the highest version's row, reduced to what a load needs.</summary>
    private static StoredRule Head(List<StoredRuleVersion> rows)
    {
        var head = rows[0];
        foreach (var row in rows)
        {
            if (row.Version > head.Version)
                head = row;
        }

        return new StoredRule(head.Name, head.Version, head.DocumentJson);
    }
}
