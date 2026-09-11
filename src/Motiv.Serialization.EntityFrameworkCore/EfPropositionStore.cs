using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The proposition store over a relational database — the twin of <see cref="EfRuleStore"/>, and
/// never written in the same transaction as it.
/// </summary>
/// <remarks>
/// <para>
/// A proposition row is replaced in place rather than appended, so the <c>(Name, Version)</c> primary
/// key <see cref="EfRuleStore"/> uses as its compare-and-set has no equivalent here. The equivalent
/// is <c>Version</c> mapped as EF's own concurrency token (see
/// <see cref="MotivStoreDbContext.OnModelCreating"/>): every generated UPDATE and DELETE carries
/// <c>AND Version = @original</c>, so a replica that committed first leaves this one matching no rows.
/// A create is guarded by the <c>Name</c> primary key, exactly as a rule append is.
/// </para>
/// <para>
/// Conflicts are detected without inspecting any provider error code, as
/// <see cref="EfRuleStore.AppendAsync"/> does. The common path reads the rows the batch names inside
/// the transaction — which is also the only way to obtain the <c>currentVersion</c> a conflict must
/// carry, since an exception cannot supply it. The race path, where another replica commits between
/// that read and this write, catches <see cref="DbUpdateException"/> — the base type, so the
/// concurrency subclass and a primary-key violation arrive at one handler — and re-reads to decide
/// whether it was a conflict or something else entirely.
/// </para>
/// <para>
/// The append-only version log the rule side has is still a deliberate asymmetry: it buys history and
/// rollback, which propositions do not offer, and is a separate question from concurrency. What is no
/// longer asymmetric is the <em>enforcement</em> — see <see cref="IPropositionStore.WriteAsync"/>.
/// </para>
/// </remarks>
public sealed class EfPropositionStore(IDbContextFactory<MotivStoreDbContext> contextFactory)
    : IPropositionStore
{
    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        using var context = contextFactory.CreateDbContext();
        var rows = context.Propositions.AsNoTracking().ToList();
        return [.. rows.Select(row => row.ToRecord())];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Propositions.AsNoTracking().ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToRecord())];
    }

    /// <inheritdoc />
    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await GenerationTracking.ReadAsync(
            context, GenerationTracking.PropositionsScope, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PropositionWriteResult> WriteAsync(
        PropositionBatch batch, CancellationToken cancellationToken)
    {
        // An empty batch is not a write — see EfRuleStore.AppendAsync for why that matters.
        if (batch.Saves.Count == 0 && batch.Deletes.Count == 0)
            return PropositionWriteResult.Written;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Tracked, not AsNoTracking: these entities carry the original Version the concurrency token
        // is compared against, and they are the ones mutated or removed below.
        var rows = await RowsNamedByAsync(context, batch, cancellationToken);

        // Rows already claimed by this batch join the claimed set as FindConflict walks, so a batch
        // that names one proposition twice is refused as the conflict it is. Without that, a
        // duplicate save would reach the change tracker and surface as an InvalidOperationException
        // from an Add outside the try — a third answer to a question the other stores answer two
        // other ways.
        if (batch.FindConflict(StoredVersion(rows)) is { } conflict)
            return conflict;

        foreach (var save in batch.Saves)
        {
            if (!rows.TryGetValue(save.Name, out var existing))
            {
                context.Propositions.Add(save.ToRow());
                continue;
            }

            existing.ModelType = save.ModelType;
            existing.DocumentJson = save.DocumentJson;
            existing.Version = save.Version;
            existing.Description = save.Description;
        }

        foreach (var deletion in batch.Deletes)
            context.Propositions.Remove(rows[deletion.Name]);

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
            // Another replica committed between the read above and this write. Roll back and ask the
            // store what happened: a version that has moved past ours means we lost the race;
            // anything else — a full disk, a dropped connection — is not a version conflict and must
            // not be reported as one.
            await transaction.RollbackAsync(cancellationToken);

            await using var fresh = await contextFactory.CreateDbContextAsync(cancellationToken);
            var raced = batch.FindConflict(
                StoredVersion(await RowsNamedByAsync(fresh, batch, cancellationToken)));
            if (raced is not null)
                return raced;

            throw;
        }
    }

    /// <summary>Every stored row the batch names, by name. One round trip decides the whole batch.</summary>
    private static async Task<Dictionary<string, PropositionRow>> RowsNamedByAsync(
        MotivStoreDbContext context, PropositionBatch batch, CancellationToken cancellationToken)
    {
        var names = batch.Saves.Select(save => save.Name)
            .Concat(batch.Deletes.Select(deletion => deletion.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var rows = await context.Propositions
            .Where(row => names.Contains(row.Name))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// The version the store holds for a name among <paramref name="rows"/>, or 0 when it holds none
    /// — the only half of the conflict predicate that is this schema's business. See
    /// <see cref="PropositionBatch.FindConflict"/> for the other half.
    /// </summary>
    private static Func<string, int> StoredVersion(Dictionary<string, PropositionRow> rows) =>
        name => rows.TryGetValue(name, out var row) ? row.Version : 0;
}
