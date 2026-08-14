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
/// </remarks>
public interface IPropositionStore
{
    /// <summary>Every persisted proposition, read once at startup. Synchronous because startup is.</summary>
    IReadOnlyList<StoredProposition> Load();

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

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
            return [.. _propositions.Values];
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
        }

        return Task.CompletedTask;
    }
}
