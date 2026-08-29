using System.Text.Json;
using Motiv.Serialization;

/// <summary>
/// Seam: rule persistence, backed by a file holding the whole append-only version log. The twin of
/// <see cref="JsonFilePropositionStore"/> — swap it for a database and nothing else changes.
/// </summary>
/// <remarks>
/// <para>
/// Rereads the file on every operation rather than caching, so two processes over one file behave
/// like two replicas over one database: the <c>(Name, Version)</c> check below really is a
/// cross-process compare-and-set, which is what makes it a useful reference implementation rather
/// than a mock. It is not, however, atomic across processes — two processes appending at exactly the
/// same instant can both read a stale file — so it is a sample store, not a production one. That is
/// what plan 2C's EF Core store is for, where the primary key is enforced by the database.
/// </para>
/// <para>
/// A torn write is a worse hazard than that lost race, though, and is closed rather than accepted: a
/// crash or a full disk part-way through overwriting the log would destroy every version of every
/// rule, not merely lose one writer's update, and would turn the fail-loud read below into an outage.
/// So <see cref="AppendAsync"/> never writes over the log in place — see
/// <see cref="WriteAllAtomically"/>.
/// </para>
/// <para>
/// The generation is derived from the log's own size rather than held in a field, so it survives a
/// restart and moves for every process — a cached counter would reset to zero on boot and break the
/// fencing token.
/// </para>
/// <para>
/// Unlike <see cref="JsonFilePropositionStore"/>, which swallows a read failure and continues with an
/// empty in-memory set, <see cref="ReadAll"/> refuses an unreadable file outright. Losing propositions
/// costs the propositions; losing the rule log would silently revert every rule to its compiled
/// default, and the very next append would overwrite the file that proved what had actually been
/// published — the history an approval gate depends on. Continuing quietly is indefensible there, so
/// this throws instead.
/// </para>
/// </remarks>
public sealed class JsonFileRuleStore(string path) : IRuleStore
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();

    /// <inheritdoc />
    public IReadOnlyList<StoredRule> Load()
    {
        lock (_gate)
        {
            return [.. ReadAll()
                .GroupBy(row => row.Name, StringComparer.Ordinal)
                .Select(rows => rows.OrderByDescending(row => row.Version).First())
                .Select(head => new StoredRule(head.Name, head.Version, head.DocumentJson))];
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    /// <inheritdoc />
    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult((long)ReadAll().Count);
    }

    /// <inheritdoc />
    public Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var log = ReadAll();
            var byName = log.ToLookup(row => row.Name, StringComparer.Ordinal);

            // Every row is checked before any is written: the batch is all-or-nothing and there is
            // no rollback here, so refusing before the write is what makes that true. Rows already
            // accepted from this batch join the taken set as it walks, so a batch repeating a
            // (Name, Version) within itself — which a hand-edited rules.json can produce — is the
            // same conflict as one repeating a version the log already holds.
            var taken = new HashSet<(string Name, int Version)>();
            foreach (var version in versions)
            {
                var existing = byName[version.Name];
                var alreadyTaken = !taken.Add((version.Name, version.Version))
                    || existing.Any(row => row.Version == version.Version);

                if (alreadyTaken)
                {
                    // Zero when the name is new: the log is at no version at all for it.
                    var current = existing.Select(row => row.Version).DefaultIfEmpty(0).Max();
                    return Task.FromResult(RuleAppendResult.Conflict(version.Name, current));
                }
            }

            log.AddRange(versions);
            WriteAllAtomically(JsonSerializer.Serialize(log, Json));
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
                [.. ReadAll().Where(row => row.Name == name).OrderBy(row => row.Version)]);
        }
    }

    /// <summary>
    /// Replaces the whole log without ever leaving a torn file at <c>path</c>. Writes a sibling temp
    /// file first, then <see cref="File.Move(string,string,bool)"/>s it into place — a same-volume
    /// rename is atomic, so a crash or a full disk can only ever be caught mid-write to the temp file,
    /// leaving the previous log intact. See the class remarks for what a bare
    /// <c>File.WriteAllText(path, ...)</c> would cost.
    /// </summary>
    private void WriteAllAtomically(string contents)
    {
        // Sibling of the target, not a system temp directory: a cross-volume Move is not atomic —
        // .NET falls back to copy-then-delete, reopening the torn-write window this exists to close.
        // Uniquely named so that a second process cannot rename this one's half-written file into place.
        var tempPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // A failed write leaves a partial temp file behind, and its name is never reused — without
            // this, the disk that filled up mid-append accumulates fresh debris on every retry. Best
            // effort: the original failure is what the caller needs to see, not a cleanup failure.
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Nothing useful to do: the append has already failed, and the leftover is inert — no
            // reader ever looks at a .tmp sibling.
        }
    }

    private List<StoredRuleVersion> ReadAll()
    {
        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<StoredRuleVersion>>(File.ReadAllText(path), Json) ?? [];
        }
        // Deliberately broad rather than an enumerated list of exception types — malformed JSON
        // (JsonException), I/O failure or a path that resolves to a directory (IOException) and
        // permission denied (UnauthorizedAccessException) are the cases known to occur here, but the
        // full set of things a filesystem can do is not knowable in advance, and every one of them
        // means the same thing: the log could not be read. See the remarks for why that is refused
        // rather than swallowed the way JsonFilePropositionStore swallows it.
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidOperationException(
                $"The rule version log at '{path}' could not be read: {exception.Message}. " +
                "Refusing to continue — appending over it would destroy the published history. " +
                "Repair or move the file.", exception);
        }
    }
}
