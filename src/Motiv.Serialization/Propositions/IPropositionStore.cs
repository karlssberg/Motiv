namespace Motiv.Serialization;

/// <summary>
/// One store round trip: everything a single publish changes. Batched rather than per-row because a
/// governed envelope publishes several propositions at once and must not be able to land half-way —
/// a failure point after the first row had been written would break "a failed persist leaves nothing
/// live".
/// </summary>
/// <remarks>
/// A name never appears in both lists — a publish either writes a row or removes it — so a store
/// need not decide which of the two would win.
/// </remarks>
/// <param name="Saves">Propositions to write, replacing any existing row of the same name.</param>
/// <param name="Deletes">Names to remove. Absent names are not an error.</param>
public sealed record PropositionBatch(
    IReadOnlyList<StoredProposition> Saves, IReadOnlyList<string> Deletes)
{
    /// <summary>A batch that writes one proposition and removes nothing.</summary>
    public static PropositionBatch Save(StoredProposition proposition) => new([proposition], []);

    /// <summary>A batch that removes one name and writes nothing.</summary>
    public static PropositionBatch Delete(string name) => new([], [name]);
}

/// <summary>
/// Where authored propositions are kept between restarts — the twin of <see cref="IRuleStore"/>. The
/// two are never written in the same transaction: each coordinates independently.
/// </summary>
/// <remarks>
/// A store is a dumb sink — it validates nothing and enforces no invariant. Legality is decided by
/// <see cref="PropositionSet"/> before anything reaches here.
/// <para>
/// <strong><see cref="LoadAsync"/> and <see cref="GetGenerationAsync"/> are forward surface,</strong>
/// the proposition-side twin of the pair <see cref="IRuleStore"/> already carries for the same reason.
/// Today only <see cref="Load"/> and <see cref="WriteAsync"/> have a production caller — a replica
/// reads the store once, at its own startup, and every write goes through <see cref="WriteAsync"/>.
/// These two exist for the background poller this plan's later tasks wire up (refreshing one replica
/// from another's write, on the generation this pair was built to support); an implementation still
/// has to honour their contracts, but for now they are contract-only — nothing in this codebase calls
/// them yet.
/// </para>
/// </remarks>
public interface IPropositionStore
{
    /// <summary>Every persisted proposition, read once at startup. Synchronous because startup is.</summary>
    IReadOnlyList<StoredProposition> Load();

    /// <summary>
    /// Every persisted proposition, read on a refresh. Separate from <see cref="Load"/> rather than
    /// replacing it because the two run at different times under different constraints: startup
    /// cannot await, a refresh can. No production caller yet — see the interface remarks on forward
    /// surface; it exists for the background poller this plan's later tasks wire up.
    /// </summary>
    Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A monotonically increasing number that moves whenever a write lands, so a replica can tell
    /// whether it is behind without re-reading anything.
    /// </summary>
    /// <remarks>
    /// <strong>Must be a scalar read.</strong> An implementation that answers this by loading the
    /// store defeats the point — every replica polls it on a timer. It must never move backwards
    /// while replicas are live: it is half of the fencing token behind monotonic-read consistency.
    /// No production caller yet — see the interface remarks on forward surface; it exists for the
    /// background poller this plan's later tasks wire up.
    /// </remarks>
    Task<long> GetGenerationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies a batch — all of it, or none. Called under the publish gate with a cancellation token,
    /// so a store that stops responding can be escaped rather than waited on forever.
    /// </summary>
    Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken);
}

/// <summary>The default store: propositions live for the lifetime of the process, as rules do.</summary>
public sealed class InMemoryPropositionStore : IPropositionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredProposition> _propositions = new(StringComparer.Ordinal);
    private long _generation;

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
            return [.. _propositions.Values];
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
    public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var proposition in batch.Saves)
                _propositions[proposition.Name] = proposition;

            foreach (var name in batch.Deletes)
                _propositions.Remove(name);

            // An empty batch is not a write. A generation that moved anyway would make every
            // replica rebuild its whole world for nothing, on a timer.
            if (batch.Saves.Count > 0 || batch.Deletes.Count > 0)
                _generation++;
        }

        return Task.CompletedTask;
    }
}
