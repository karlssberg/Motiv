using System.Text.Json;
using Motiv.Serialization;

/// <summary>
/// Seam: proposition persistence. The library keeps authored propositions in memory and delegates
/// durability to a store, exactly as it delegates transport — swap this for a database and nothing
/// else changes.
/// </summary>
/// <remarks>
/// Rewrites the whole file on every write. Authoring is a human-paced operation, so the simplicity is
/// worth more here than incremental writes would be. Calls arrive while the publish gate is held, so
/// this must stay quick. The trade-off this simplicity buys: a crash or full disk mid-write truncates
/// the file, and the next <see cref="Load"/> then silently drops every proposition rather than just
/// the one that was being written — acceptable for a sample, but worth knowing.
/// <para>
/// Because <see cref="WriteAsync"/> rewrites whatever <c>ReadAll</c> returned, an unreadable file is
/// not merely skipped — the next write replaces it with the contents read past it, and the original
/// is gone. Unlike the library's quarantine, which retains a bad document for repair, nothing here
/// retains a bad *file*. So the read failure is reported on <see cref="Console.Error"/> the moment it
/// happens: a real store would log it, refuse to write over an unread file, or both.
/// </para>
/// <para>
/// The generation mirrors <c>JsonFileRuleStore</c>'s: the file's own row count, read fresh rather
/// than cached, so it survives a restart and moves for every process the way the rule store's does.
/// It is a coarser signal here than on the rule log, though — that log is append-only, so its count
/// grows on every accepted write; this store overwrites a row of the same name in place, so a batch
/// that only replaces existing rows leaves the count, and therefore the generation, unchanged. A
/// poller would then not know a replace happened until some other write also changed the row count.
/// Acceptable for a sample twin of the rule store; a production store would derive this from
/// something that moves on every write, such as a row version or the file's last-write time.
/// </para>
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
    public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    /// <inheritdoc />
    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult((long)ReadAll().Count);
    }

    /// <inheritdoc />
    public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            // Every name the batch speaks for, whether to replace it or drop it. One set rather than
            // two lookups: a rewritten file keeps the rows the batch says nothing about, then appends
            // the saves.
            var superseded = new HashSet<string>(batch.Deletes, StringComparer.Ordinal);
            foreach (var proposition in batch.Saves)
                superseded.Add(proposition.Name);

            Write([.. ReadAll().Where(existing => !superseded.Contains(existing.Name)), .. batch.Saves]);
        }

        return Task.CompletedTask;
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
            //
            // Swallowed, but never silent — see the remarks for why an unreported read failure is
            // destructive here. Console.Error keeps the sample free of a logging dependency; a real
            // store would use ILogger.
            Console.Error.WriteLine(
                $"[JsonFilePropositionStore] Could not read '{path}': {exception.Message}\n" +
                "  Continuing with no stored propositions. The next save will OVERWRITE this file — " +
                "copy it aside now if you intend to repair it.");
            return [];
        }
    }

    private void Write(List<StoredProposition> propositions) =>
        File.WriteAllText(path, JsonSerializer.Serialize(propositions, Json));
}
