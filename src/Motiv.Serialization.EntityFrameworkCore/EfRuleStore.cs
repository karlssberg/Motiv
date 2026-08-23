using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The rule store over a relational database, where the <c>(Name, Version)</c> primary key is
/// enforced by the database rather than by a re-read of a file.
/// </summary>
/// <remarks>
/// <para>
/// Conflicts are detected without inspecting any provider error code. The common path reads the
/// versions already taken inside the transaction — which is also the only way to obtain the
/// <c>currentVersion</c> a conflict must carry, since an exception cannot supply it. The race path,
/// where another replica commits between that read and the insert, catches
/// <see cref="DbUpdateException"/> and re-reads to decide whether it was a conflict or something
/// else entirely. That is what makes proving this store on SQLite generalise to PostgreSQL and SQL
/// Server: the only behaviour relied on is EF's own.
/// </para>
/// <para>
/// A fresh context per operation, because the store is a singleton and <see cref="DbContext"/> is
/// not thread-safe — and because it structurally guarantees this store never shares a transaction
/// with the proposition store.
/// </para>
/// </remarks>
public sealed class EfRuleStore(IDbContextFactory<MotivStoreDbContext> contextFactory) : IRuleStore
{
    /// <inheritdoc />
    public IReadOnlyList<StoredRule> Load()
    {
        using var context = contextFactory.CreateDbContext();
        return HeadQuery(context).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await HeadQuery(context).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await GenerationTracking.ReadAsync(
            context, GenerationTracking.RulesScope, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken)
    {
        // An empty batch is not a write: moving the generation would make every replica rebuild its
        // whole world, on a timer, for nothing.
        if (versions.Count == 0)
            return RuleAppendResult.Appended;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var conflict = await FindConflictAsync(context, versions, cancellationToken);
        if (conflict is not null)
            return conflict;

        foreach (var version in versions)
            context.RuleVersions.Add(version.ToRow());

        await GenerationTracking.BumpAsync(context, GenerationTracking.RulesScope, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RuleAppendResult.Appended;
        }
        catch (DbUpdateException)
        {
            // Another replica committed between the read above and this insert. Roll back and ask
            // the store what happened: a row of ours now present means we lost the race; anything
            // else — a full disk, a dropped connection — is not a version conflict and must not be
            // reported as one.
            await transaction.RollbackAsync(cancellationToken);

            await using var fresh = await contextFactory.CreateDbContextAsync(cancellationToken);
            var raced = await FindConflictAsync(fresh, versions, cancellationToken);
            if (raced is not null)
                return raced;

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.RuleVersions.AsNoTracking()
            .Where(row => row.Name == name)
            .OrderBy(row => row.Version)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => row.ToRecord())];
    }

    /// <summary>
    /// The head projection, expressed as SQL: every log row that no higher version of the same name
    /// supersedes.
    /// </summary>
    /// <remarks>
    /// Still a projection and never a stored duplicate — the change is only <em>where</em> it is
    /// computed. Materialising the whole log to take a per-name maximum client-side made the central
    /// read path cost O(the entire append-only log), on every replica, on every generation bump, for
    /// a log that is never pruned. The <c>NOT EXISTS</c> this compiles to is checked against all
    /// three providers by <c>ProviderSchemaTests</c>; <c>(Name, Version)</c> being the primary key
    /// is what guarantees it selects exactly one row per name.
    /// </remarks>
    internal static IQueryable<StoredRule> HeadQuery(MotivStoreDbContext context) =>
        context.RuleVersions.AsNoTracking()
            .Where(row => !context.RuleVersions
                .Any(other => other.Name == row.Name && other.Version > row.Version))
            .Select(row => new StoredRule(row.Name, row.Version, row.DocumentJson));

    /// <summary>
    /// The first batch row whose version is already taken, or null when the batch is clear. Reads
    /// every name at once: the batch is all-or-nothing, so one round trip decides the whole thing.
    /// </summary>
    /// <remarks>
    /// Rows already accepted from <em>this</em> batch join the taken set as it walks, so a batch
    /// that repeats a <c>(Name, Version)</c> within itself is refused as the conflict it is. Without
    /// that, the duplicate would reach the change tracker and surface as an
    /// <see cref="InvalidOperationException"/> from an <c>Add</c> that sits outside the try — a
    /// third answer to a question the other stores answer two other ways.
    /// </remarks>
    private static async Task<RuleAppendResult?> FindConflictAsync(
        MotivStoreDbContext context,
        IReadOnlyList<StoredRuleVersion> versions,
        CancellationToken cancellationToken)
    {
        var names = versions.Select(version => version.Name).Distinct(StringComparer.Ordinal).ToList();

        var existing = await context.RuleVersions.AsNoTracking()
            .Where(row => names.Contains(row.Name))
            .Select(row => new { row.Name, row.Version })
            .ToListAsync(cancellationToken);

        var taken = new HashSet<(string Name, int Version)>(
            existing.Select(row => (row.Name, row.Version)));

        var highest = existing
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(row => row.Version), StringComparer.Ordinal);

        foreach (var version in versions)
        {
            if (!taken.Add((version.Name, version.Version)))
            {
                // Zero when the name is new: the store is at no version at all for it.
                highest.TryGetValue(version.Name, out var current);
                return RuleAppendResult.Conflict(version.Name, current);
            }
        }

        return null;
    }
}
