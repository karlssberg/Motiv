using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>The scope keys of the two <see cref="StoreGenerationRow"/> rows.</summary>
internal static class GenerationScopes
{
    public const string Rules = "rules";
    public const string Propositions = "propositions";
}

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
        return ProjectHeads(context.RuleVersions.AsNoTracking().ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return ProjectHeads(
            await context.RuleVersions.AsNoTracking().ToListAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadGenerationAsync(context, GenerationScopes.Rules, cancellationToken);
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

        await BumpGenerationAsync(context, GenerationScopes.Rules, cancellationToken);

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
    /// The first batch row whose version is already taken, or null when the batch is clear. Reads
    /// every name at once: the batch is all-or-nothing, so one round trip decides the whole thing.
    /// </summary>
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

        if (existing.Count == 0)
            return null;

        var taken = new HashSet<(string Name, int Version)>(
            existing.Select(row => (row.Name, row.Version)));

        var highest = existing
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(row => row.Version), StringComparer.Ordinal);

        foreach (var version in versions)
        {
            if (taken.Contains((version.Name, version.Version)))
                return RuleAppendResult.Conflict(version.Name, highest[version.Name]);
        }

        return null;
    }

    /// <summary>The head projection: the highest version per name, reduced to what a load needs.</summary>
    private static IReadOnlyList<StoredRule> ProjectHeads(List<RuleVersionRow> rows) =>
    [
        .. rows
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .Select(group => group.Aggregate((head, row) => row.Version > head.Version ? row : head))
            .Select(head => new StoredRule(head.Name, head.Version, head.DocumentJson))
    ];

    private static async Task<long> ReadGenerationAsync(
        MotivStoreDbContext context, string scope, CancellationToken cancellationToken)
    {
        var row = await context.StoreGenerations.AsNoTracking()
            .SingleOrDefaultAsync(generation => generation.Scope == scope, cancellationToken);

        return row?.Generation ?? 0;
    }

    /// <summary>
    /// Moves this store's generation, tracked so the increment is written by the caller's
    /// <c>SaveChangesAsync</c> — the bump and the write it describes land in one transaction.
    /// </summary>
    internal static async Task BumpGenerationAsync(
        MotivStoreDbContext context, string scope, CancellationToken cancellationToken)
    {
        var row = await context.StoreGenerations
            .SingleOrDefaultAsync(generation => generation.Scope == scope, cancellationToken);

        if (row is null)
            context.StoreGenerations.Add(new StoreGenerationRow { Scope = scope, Generation = 1 });
        else
            row.Generation++;
    }
}
