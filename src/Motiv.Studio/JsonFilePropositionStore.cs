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
/// The generation deliberately does <em>not</em> mirror <c>JsonFileRuleStore</c>'s, even though the
/// two stores otherwise share a shape. <c>JsonFileRuleStore</c> is an append-only version log —
/// <c>AppendAsync</c> only ever adds rows, so the file's row count is monotonic and moves on every
/// accepted write, which is what makes it a valid generation there. This store instead replaces rows
/// in place: <see cref="WriteAsync"/> drops every superseded name and re-appends the saves, so saving
/// a changed document under an <em>existing</em> name — editing a proposition, the common case —
/// leaves the row count identical. Row count is therefore not transferable between an append-only
/// store and a replace store; using it here would mean a poller could observe creates and deletes but
/// never an edit to an existing proposition, which is exactly the case Spec 2B's refresh exists to
/// converge on. So the generation here is instead the file's last-write time in UTC ticks, or
/// <c>0</c> when the file does not exist — it moves on every write regardless of whether the row
/// count changed.
/// </para>
/// <para>
/// Deriving the generation from mtime instead of a held counter has a consequence
/// <see cref="InMemoryPropositionStore"/> does not share: <c>File.WriteAllText</c> bumps a file's
/// mtime whether or not the bytes it wrote differ from what was already there, so any rewrite would
/// move the generation — including one for an empty batch that changed nothing. <see cref="WriteAsync"/>
/// therefore returns before touching the file at all when the batch is empty, the same invariant
/// <see cref="InMemoryPropositionStore.WriteAsync"/> enforces with its counter: an empty batch is not
/// a write, and a generation that moved for one would make every replica rebuild its whole world for
/// nothing, on a timer.
/// </para>
/// <para>
/// Raw <c>File.GetLastWriteTimeUtc</c> resolution is not trustworthy on its own: it is platform-
/// dependent, and two writes landing close enough together can read back an identical mtime — this
/// is exactly what let two writes through without the generation moving on Windows CI, invisibly on
/// macOS/APFS's finer resolution. <see cref="WriteAsync"/> therefore reads the mtime before it
/// writes and, if the write did not push it strictly past that reading, sets it forward explicitly —
/// see <c>EnsureGenerationMovedPast</c> for the rejected alternatives. This is still a sample-grade
/// answer, not a production one: the guarantee only applies across writes made through this store,
/// so a system clock moving backwards with no write in between can still move the generation
/// backwards, something <see cref="IPropositionStore.GetGenerationAsync"/> promises it never does
/// while replicas are live. Plan 2C's EF Core store is the real one, where the primary key and a
/// proper row version close that gap too; this file exists so two processes over one path behave
/// like two replicas, not to be that store itself.
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
            return Task.FromResult(CurrentGeneration());
    }

    /// <inheritdoc />
    public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        // An empty batch is not a write. Now that the generation is the file's mtime rather than a
        // held counter, rewriting the file here would bump it even though nothing changed —
        // File.WriteAllText touches mtime regardless of whether the bytes it wrote differ from what
        // was already there. A poller would then rebuild its whole world for nothing, on a timer.
        if (batch.Saves.Count == 0 && batch.Deletes.Count == 0)
            return Task.CompletedTask;

        lock (_gate)
        {
            // Every name the batch speaks for, whether to replace it or drop it. One set rather than
            // two lookups: a rewritten file keeps the rows the batch says nothing about, then appends
            // the saves.
            var superseded = new HashSet<string>(batch.Deletes, StringComparer.Ordinal);
            foreach (var proposition in batch.Saves)
                superseded.Add(proposition.Name);

            var previousGeneration = CurrentGeneration();

            Write([.. ReadAll().Where(existing => !superseded.Contains(existing.Name)), .. batch.Saves]);

            EnsureGenerationMovedPast(previousGeneration);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The generation this store hands out: the file's last-write time in UTC ticks, or <c>0</c> when
    /// the file does not exist. Read in one place so the value <see cref="WriteAsync"/> compares
    /// against is the same one <see cref="GetGenerationAsync"/> publishes.
    /// </summary>
    private long CurrentGeneration() => File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;

    /// <summary>
    /// Makes this store the authority on its own monotonicity rather than a hostage to the
    /// filesystem's timestamp resolution — see the class remarks for why the raw mtime cannot be
    /// trusted to have moved on its own.
    /// </summary>
    private void EnsureGenerationMovedPast(long previousGeneration)
    {
        // Alternatives considered and rejected:
        //  - A content hash moves on any change but is not monotonic — a generation that can go
        //    *down* would trip the TypeScript client's backwards-move detector on a perfectly
        //    correct response, a false alarm that trains users to ignore the signal.
        //  - A counter persisted inside the file would work and is arguably cleaner, but it changes
        //    the on-disk format, and Studio ships a seed file that must still load without one.
        //    Not worth it for a sample store.
        if (CurrentGeneration() <= previousGeneration)
            File.SetLastWriteTimeUtc(path, new DateTime(previousGeneration + 1, DateTimeKind.Utc));
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
            // destructive here. Console.Error keeps this sample store free of a logging
            // dependency; a real store would use ILogger.
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
