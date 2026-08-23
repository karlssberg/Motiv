using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The proposition store over a relational database — the twin of <see cref="EfRuleStore"/>, and
/// never written in the same transaction as it.
/// </summary>
/// <remarks>
/// There is no conflict outcome here, because the contract has none: a proposition row is replaced
/// in place, last writer wins, exactly as <c>InMemoryPropositionStore</c> behaves. The append-only
/// version log the rule side has is a deliberate asymmetry, deferred to its own spec because closing
/// it is a breaking change to <see cref="IPropositionStore"/>.
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
    public async Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        // An empty batch is not a write — see EfRuleStore.AppendAsync for why that matters.
        if (batch.Saves.Count == 0 && batch.Deletes.Count == 0)
            return;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var save in batch.Saves)
        {
            var existing = await context.Propositions
                .SingleOrDefaultAsync(row => row.Name == save.Name, cancellationToken);

            if (existing is null)
            {
                context.Propositions.Add(save.ToRow());
                continue;
            }

            existing.ModelType = save.ModelType;
            existing.DocumentJson = save.DocumentJson;
            existing.Version = save.Version;
            existing.Description = save.Description;
        }

        foreach (var name in batch.Deletes)
        {
            var existing = await context.Propositions
                .SingleOrDefaultAsync(row => row.Name == name, cancellationToken);

            // An absent name is not an error: the store is a dumb sink, and the set decides legality.
            if (existing is not null)
                context.Propositions.Remove(existing);
        }

        await GenerationTracking.BumpAsync(
            context, GenerationTracking.PropositionsScope, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
