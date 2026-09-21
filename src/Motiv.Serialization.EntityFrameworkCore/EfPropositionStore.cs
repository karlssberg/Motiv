using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The proposition store over an append-only version log, the twin of <see cref="EfRuleStore"/>.
/// A save inserts a row; a deletion inserts a tombstone; the <c>(Name, Version)</c> primary key is
/// the compare-and-set, so the read-then-catch shape below is sound without a concurrency token.
/// </summary>
public sealed class EfPropositionStore(IDbContextFactory<MotivStoreDbContext> contextFactory)
    : IPropositionStore
{
    public IReadOnlyList<StoredProposition> Load()
    {
        using var context = contextFactory.CreateDbContext();
        return HeadQuery(context).ToList();
    }

    public async Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await HeadQuery(context).ToListAsync(cancellationToken);
    }

    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await GenerationTracking.ReadAsync(
            context, GenerationTracking.PropositionsScope, cancellationToken);
    }

    public async Task<PropositionWriteResult> WriteAsync(
        PropositionBatch batch, CancellationToken cancellationToken)
    {
        // An empty batch is not a write — see EfRuleStore.AppendAsync for why that matters.
        if (batch.IsEmpty)
            return PropositionWriteResult.Written;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var positions = await PositionsNamedByAsync(context, batch, cancellationToken);
        if (batch.FindConflict(PositionIn(positions)) is { } conflict)
            return conflict;

        var now = DateTimeOffset.UtcNow;

        foreach (var save in batch.Saves)
            context.PropositionVersions.Add(StoredPropositionVersion.Saved(save, batch.Provenance, now).ToRow());

        foreach (var deletion in batch.Deletes)
        {
            positions.TryGetValue(deletion.Name, out var retired);
            context.PropositionVersions.Add(
                StoredPropositionVersion.Tombstone(deletion, retired?.ModelType, batch.Provenance, now).ToRow());
        }

        await GenerationTracking.BumpAsync(
            context, GenerationTracking.PropositionsScope, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PropositionWriteResult.Written;
        }
        catch (DbUpdateException)
        {
            // Another replica committed between the read above and this insert. Roll back and ask
            // the store what happened: a position that has moved past ours means we lost the race;
            // anything else — a full disk, a dropped connection — is not a version conflict and must
            // not be reported as one.
            await transaction.RollbackAsync(cancellationToken);

            await using var fresh = await contextFactory.CreateDbContextAsync(cancellationToken);
            var raced = batch.FindConflict(
                PositionIn(await PositionsNamedByAsync(fresh, batch, cancellationToken)));
            if (raced is not null)
                return raced;

            throw;
        }
    }

    public async Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.PropositionVersions.AsNoTracking()
            .Where(row => row.Name == name)
            .OrderBy(row => row.Version)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => row.ToRecord())];
    }

    /// <summary>
    /// The live heads: the highest-versioned row of each name, when that row carries a document.
    /// Computed by the database — superseded rows and tombstones never leave it.
    /// </summary>
    internal static IQueryable<StoredProposition> HeadQuery(MotivStoreDbContext context) =>
        context.PropositionVersions.AsNoTracking()
            .Where(row => row.DocumentJson != null
                && !context.PropositionVersions
                    .Any(other => other.Name == row.Name && other.Version > row.Version))
            .Select(row => new StoredProposition(
                row.Name, row.ModelType!, row.DocumentJson!, row.Version, row.Description));

    /// <summary>The highest row of every name the batch speaks for — enough to place and to tombstone.</summary>
    private static async Task<Dictionary<string, HighestRow>> PositionsNamedByAsync(
        MotivStoreDbContext context, PropositionBatch batch, CancellationToken cancellationToken)
    {
        var names = batch.Saves.Select(save => save.Name)
            .Concat(batch.Deletes.Select(deletion => deletion.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var rows = await context.PropositionVersions.AsNoTracking()
            .Where(row => names.Contains(row.Name))
            .Select(row => new { row.Name, row.Version, row.ModelType, Live = row.DocumentJson != null })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var highest = group.MaxBy(row => row.Version)!;
                    return new HighestRow(new PropositionPosition(highest.Version, highest.Live), highest.ModelType);
                },
                StringComparer.Ordinal);
    }

    private static Func<string, PropositionPosition?> PositionIn(Dictionary<string, HighestRow> positions) =>
        name => positions.TryGetValue(name, out var row) ? row.Position : null;

    private sealed record HighestRow(PropositionPosition Position, string? ModelType);
}
