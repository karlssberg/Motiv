using System.Text.Json;
using Motiv.Serialization;

/// <summary>
/// Seam: proposition persistence. The library keeps authored propositions in memory and delegates
/// durability to a store, exactly as it delegates transport — swap this for a database and nothing
/// else changes.
/// </summary>
/// <remarks>
/// Rewrites the whole file on every save. Authoring is a human-paced operation, so the simplicity is
/// worth more here than incremental writes would be. Calls arrive while the publish lock is held, so
/// this must stay quick. The trade-off this simplicity buys: a crash or full disk mid-write truncates
/// the file, and the next <see cref="Load"/> then silently drops every proposition rather than just
/// the one that was being written — acceptable for a sample, but worth knowing.
/// </remarks>
public sealed class JsonFilePropositionStore(string path) : IPropositionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
            return ReadAll();
    }

    /// <inheritdoc />
    public void Save(StoredProposition proposition)
    {
        lock (_gate)
        {
            var propositions = ReadAll().Where(existing => existing.Name != proposition.Name).ToList();
            propositions.Add(proposition);
            Write(propositions);
        }
    }

    /// <inheritdoc />
    public void Delete(string name)
    {
        lock (_gate)
            Write([.. ReadAll().Where(existing => existing.Name != name)]);
    }

    private List<StoredProposition> ReadAll()
    {
        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<StoredProposition>>(File.ReadAllText(path), Json) ?? [];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A hand-edited or half-written file must not stop the app booting. The library
            // quarantines documents that fail to bind; an unreadable file is the same problem one
            // layer down, and the same answer applies. Deliberately broad rather than an
            // enumerated list of exception types — malformed JSON (JsonException), I/O failure or
            // a path that resolves to a directory (IOException), and permission denied
            // (UnauthorizedAccessException) are the cases known to occur here, but the point of
            // "Load must never throw" is that the full set of things a filesystem can do is not
            // knowable in advance.
            return [];
        }
    }

    private void Write(List<StoredProposition> propositions) =>
        File.WriteAllText(path, JsonSerializer.Serialize(propositions, Json));
}
