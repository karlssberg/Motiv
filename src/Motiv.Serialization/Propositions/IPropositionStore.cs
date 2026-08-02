namespace Motiv.Serialization;

/// <summary>
/// Where authored propositions are kept between restarts. Deliberately narrow and synchronous, to
/// match the synchronous publish path: implementations are called while the publish lock is held, so
/// they must be quick.
/// </summary>
/// <remarks>
/// A store is a dumb sink — it validates nothing and enforces no invariants. Legality is decided by
/// <see cref="PropositionSet"/> before anything reaches here.
/// </remarks>
public interface IPropositionStore
{
    /// <summary>Every persisted proposition, read once at startup.</summary>
    IReadOnlyList<StoredProposition> Load();

    /// <summary>Persists a proposition, replacing any existing one of the same name.</summary>
    void Save(StoredProposition proposition);

    /// <summary>Removes a proposition, doing nothing when the name is absent.</summary>
    void Delete(string name);
}

/// <summary>The default store: propositions live for the lifetime of the process, as rules do.</summary>
public sealed class InMemoryPropositionStore : IPropositionStore
{
    private readonly Dictionary<string, StoredProposition> _propositions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load() => [.. _propositions.Values];

    /// <inheritdoc />
    public void Save(StoredProposition proposition) => _propositions[proposition.Name] = proposition;

    /// <inheritdoc />
    public void Delete(string name) => _propositions.Remove(name);
}
