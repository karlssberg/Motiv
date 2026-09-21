namespace Motiv.Serialization;

/// <summary>
/// One name a batch removes, and the version the writer last observed it at.
/// </summary>
/// <remarks>
/// The version is not decoration: it is the compare-and-set half of a removal, exactly as the row's
/// own <see cref="StoredProposition.Version"/> is for a save. A store refuses a deletion whose
/// version is not the one it holds — including the case where it holds no row at all, which is a
/// removal that another writer already performed.
/// </remarks>
/// <param name="Name">The dot-separated name to remove.</param>
/// <param name="Version">The version the writer observed, and expects the store to still be at.</param>
public sealed record PropositionDeletion(string Name, int Version);

/// <summary>
/// One store round trip: everything a single publish changes, and the provenance every row it writes
/// is stamped with. Batched rather than per-row because a governed envelope publishes several
/// propositions at once and must not be able to land half-way — a failure point after the first row
/// had been written would break "a failed persist leaves nothing live".
/// </summary>
/// <remarks>
/// A name never appears in both lists, and never twice in either — a publish either writes a row or
/// retires it, once. A store need not decide which of two entries for one name would win; it refuses
/// the batch, because a batch that cannot say what it wants is the same stale-writer signal as one
/// that wants something the store has moved past.
/// </remarks>
/// <param name="Saves">Propositions to write, each a new row in its name's version log.</param>
/// <param name="Deletes">Names to retire, each with the version the writer observed.</param>
/// <param name="Provenance">Who is writing, and why — stamped on every row the batch produces.</param>
public sealed record PropositionBatch(
    IReadOnlyList<StoredProposition> Saves,
    IReadOnlyList<PropositionDeletion> Deletes,
    RuleChangeProvenance Provenance)
{
    /// <summary>A batch attributed to the system — what an import or a test writes.</summary>
    public PropositionBatch(IReadOnlyList<StoredProposition> saves, IReadOnlyList<PropositionDeletion> deletes)
        : this(saves, deletes, RuleChangeProvenance.System)
    {
    }

    /// <summary>
    /// Whether the batch asks for nothing. An empty batch is not a write: a store must leave its
    /// generation where it stands, or every replica rebuilds its whole world for nothing, on a timer.
    /// </summary>
    public bool IsEmpty => Saves.Count == 0 && Deletes.Count == 0;

    /// <summary>A batch that writes one proposition and retires nothing.</summary>
    public static PropositionBatch Save(StoredProposition proposition, RuleChangeProvenance? provenance = null) =>
        new([proposition], [], provenance ?? RuleChangeProvenance.System);

    /// <summary>A batch that retires one name at the version the writer observed, and writes nothing.</summary>
    public static PropositionBatch Delete(string name, int version, RuleChangeProvenance? provenance = null) =>
        new([], [new PropositionDeletion(name, version)], provenance ?? RuleChangeProvenance.System);

    /// <summary>
    /// The first entry whose version is not one the store can accept, or null when the batch is clear
    /// — the compare-and-set <see cref="IPropositionStore.WriteAsync"/> documents, written once so
    /// that three stores in three assemblies cannot answer it three ways.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="position"/> is the only thing a store contributes: the highest version its log
    /// holds for a name — tombstones included — and whether that row is live, or null when it has
    /// never held the name. Everything else about the predicate is the contract, not the schema: a
    /// save must land strictly past the highest version, so a re-created name never reuses a number
    /// the decision log may pin; a deletion must name the live head exactly, so an absent name, a
    /// tombstoned one or a stale version is refused as a writer acting on a row someone else moved.
    /// </para>
    /// <para>
    /// Names already claimed by <em>this</em> batch join the claimed set as it walks, so a batch
    /// naming one proposition twice is refused as the conflict it is rather than reaching the store
    /// and surfacing as whatever that store does with a duplicate.
    /// </para>
    /// <para>
    /// A save or a deletion at a non-positive version is refused before the lookup is consulted: no
    /// row has ever carried version 0, so no deletion can honestly name it and no save can start there.
    /// </para>
    /// <para>
    /// Call it before writing anything: the batch is all-or-nothing, and in a store with no rollback
    /// refusing up front is what makes that true.
    /// </para>
    /// </remarks>
    /// <param name="position">The store's position for a name, or null when it has never held it.</param>
    public PropositionWriteResult? FindConflict(Func<string, PropositionPosition?> position)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var save in Saves)
        {
            var current = position(save.Name);
            if (!claimed.Add(save.Name) || save.Version < 1 || (current is not null && save.Version <= current.Version))
                return PropositionWriteResult.Conflict(save.Name, current?.Version ?? 0);
        }

        foreach (var deletion in Deletes)
        {
            var current = position(deletion.Name);
            if (!claimed.Add(deletion.Name)
                || deletion.Version < 1
                || current is not { Live: true }
                || deletion.Version != current.Version)
                return PropositionWriteResult.Conflict(deletion.Name, current?.Version ?? 0);
        }

        return null;
    }
}

/// <summary>
/// The outcome of an <see cref="IPropositionStore.WriteAsync"/> — the proposition-side twin of
/// <see cref="RuleAppendResult"/>. A conflict is an expected outcome, a second writer arriving on a
/// stale basis, so it is a value rather than an exception.
/// </summary>
public sealed class PropositionWriteResult
{
    private PropositionWriteResult(bool isConflict, string? name, int currentVersion)
    {
        IsConflict = isConflict;
        Name = name;
        CurrentVersion = currentVersion;
    }

    /// <summary>Whether the batch was refused because an entry's version was not the one the store holds.</summary>
    public bool IsConflict { get; }

    /// <summary>The proposition whose version was stale, or null when nothing conflicted.</summary>
    public string? Name { get; }

    /// <summary>The version that name is actually at, or 0 when the store holds no row for it.</summary>
    public int CurrentVersion { get; }

    /// <summary>Every entry landed.</summary>
    public static PropositionWriteResult Written { get; } = new(false, null, 0);

    /// <summary>Nothing landed: <paramref name="name"/> is actually at <paramref name="currentVersion"/>.</summary>
    public static PropositionWriteResult Conflict(string name, int currentVersion) =>
        new(true, name, currentVersion);
}

/// <summary>
/// Where authored propositions are kept between restarts — the twin of <see cref="IRuleStore"/>. The
/// two are never written in the same transaction: each coordinates independently.
/// </summary>
/// <remarks>
/// A store is a dumb sink for <em>semantic</em> legality — it validates no document and enforces no
/// proposition-level invariant; <see cref="PropositionSet"/> decides all of that before anything
/// reaches here. It is not, however, dumb about <em>structure</em>: a name's version is the
/// compare-and-set that makes a lost update impossible across processes, and
/// <see cref="WriteAsync"/> reporting a conflict is how a stale writer finds out. See that method for
/// the predicate, and why it is the same one <see cref="IRuleStore.AppendAsync"/> enforces with a
/// primary key.
/// <para>
/// <see cref="LoadAsync"/> and <see cref="GetGenerationAsync"/> — the proposition-side twin of the
/// pair <see cref="IRuleStore"/> carries, for the same reason — back
/// <see cref="PropositionSet.RefreshAsync"/>: a replica polls <see cref="GetGenerationAsync"/> on a
/// timer (see <c>Motiv.Serialization.AspNetCore.MotivRefreshService</c>, an opt-in background poller)
/// and only calls <see cref="LoadAsync"/> — the expensive rebuild path — once that scalar has actually
/// moved. <see cref="GetGenerationAsync"/> is also read straight after every write, so the successor
/// generation records where the store stood in the same swap that publishes what it stands on.
/// </para>
/// </remarks>
public interface IPropositionStore
{
    /// <summary>Every persisted proposition, read once at startup. Synchronous because startup is.</summary>
    IReadOnlyList<StoredProposition> Load();

    /// <summary>
    /// Every persisted proposition, read on a refresh. Separate from <see cref="Load"/> rather than
    /// replacing it because the two run at different times under different constraints: startup
    /// cannot await, a refresh can. Called by <see cref="PropositionSet.RefreshAsync"/>, and only once
    /// <see cref="GetGenerationAsync"/> has shown the store moved — see the interface remarks.
    /// </summary>
    Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A monotonically increasing number that moves whenever a write lands, so a replica can tell
    /// whether it is behind without re-reading anything.
    /// </summary>
    /// <remarks>
    /// <strong>Must be a scalar read.</strong> An implementation that answers this by loading the
    /// store defeats the point — every replica polls it on a timer, via
    /// <see cref="PropositionSet.RefreshAsync"/> and, opt-in,
    /// <c>Motiv.Serialization.AspNetCore.MotivRefreshService</c>. It must never move backwards while
    /// replicas are live: it is half of the fencing token behind monotonic-read consistency.
    /// </remarks>
    Task<long> GetGenerationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies a batch — all of it, or none. Called under the publish gate with a cancellation token,
    /// so a store that stops responding can be escaped rather than waited on forever.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The store is an append-only version log keyed on <c>(Name, Version)</c>, exactly as
    /// <see cref="IRuleStore"/> is. A save inserts a row. A deletion inserts a <em>tombstone</em>: a
    /// row one past the deleted version with no document, so <see cref="Load"/> no longer reports the
    /// name while <see cref="HistoryAsync"/> still records who withdrew it and when — and a later
    /// re-creation continues the numbering rather than reusing a version the decision log may pin.
    /// </para>
    /// <para>
    /// <strong>Every entry is a compare-and-set against the position the store holds for that name</strong>,
    /// and one stale entry refuses the whole batch: a save must be <em>strictly past</em> the highest
    /// version the log holds, tombstones included; a deletion must <em>equal</em> the live head. The
    /// key is what enforces it across processes: two replicas that both read v5 and both publish v6
    /// race on the insert, one lands, and the other is told the current version and can rebase.
    /// </para>
    /// <para>
    /// This is the only invariant a store enforces, and it is structural rather than semantic. It is
    /// enforced here because it cannot be enforced anywhere else: an in-process check compares against
    /// this replica's memory, which is silent about every other replica.
    /// </para>
    /// <para>
    /// An implementation need not re-derive the predicate: <see cref="PropositionBatch.FindConflict"/>
    /// applies it to a batch given only the position this store holds for a name. Every store shipped
    /// here uses it, which is what keeps their refusals identical. Every row a store writes is built
    /// by <see cref="StoredPropositionVersion.Saved"/> or <see cref="StoredPropositionVersion.Tombstone"/>
    /// from the batch's <see cref="PropositionBatch.Provenance"/>, stamped with the store's clock.
    /// </para>
    /// </remarks>
    Task<PropositionWriteResult> WriteAsync(PropositionBatch batch, CancellationToken cancellationToken);

    /// <summary>
    /// Every version the log holds for <paramref name="name"/>, in version order, tombstones included.
    /// Empty for a name the store has never held. Kept forever: this is what a decision record's pinned
    /// proposition version resolves against.
    /// </summary>
    Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(string name, CancellationToken cancellationToken);
}

/// <summary>The default store: propositions live for the lifetime of the process, as rules do.</summary>
/// <remarks>
/// Real, not a stub — it keeps the same log and enforces the same version compare-and-set, so the
/// conflict path this store produces is the one a database store produces, and a test written against
/// it holds against Postgres.
/// </remarks>
public sealed class InMemoryPropositionStore : IPropositionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<StoredPropositionVersion>> _log = new(StringComparer.Ordinal);
    private long _generation;

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
        {
            return StoredPropositionVersion.HeadsOf(_log.Values);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    /// <inheritdoc />
    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult(_generation);
    }

    /// <inheritdoc />
    public Task<PropositionWriteResult> WriteAsync(
        PropositionBatch batch, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (batch.FindConflict(Position) is { } conflict)
                return Task.FromResult(conflict);

            var now = DateTimeOffset.UtcNow;

            foreach (var proposition in batch.Saves)
                Rows(proposition.Name).Add(StoredPropositionVersion.Saved(proposition, batch.Provenance, now));

            foreach (var deletion in batch.Deletes)
            {
                var rows = Rows(deletion.Name);
                var modelType = StoredPropositionVersion.HeadOf(rows)?.ModelType;
                rows.Add(StoredPropositionVersion.Tombstone(deletion, modelType, batch.Provenance, now));
            }

            if (!batch.IsEmpty)
                _generation++;

            return Task.FromResult(PropositionWriteResult.Written);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredPropositionVersion>>(
                _log.TryGetValue(name, out var rows)
                    ? [.. rows.OrderBy(row => row.Version)]
                    : []);
        }
    }

    private PropositionPosition? Position(string name) =>
        _log.TryGetValue(name, out var rows) ? StoredPropositionVersion.PositionOf(rows) : null;

    private List<StoredPropositionVersion> Rows(string name)
    {
        if (!_log.TryGetValue(name, out var rows))
            _log[name] = rows = [];
        return rows;
    }
}
