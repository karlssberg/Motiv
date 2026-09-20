using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>The scenario store over one table replaced in place, with the version as a concurrency token.</summary>
public sealed class EfScenarioStore(IDbContextFactory<MotivStoreDbContext> contextFactory) : IScenarioStore
{
    public async Task<IReadOnlyList<StoredScenario>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Scenarios.AsNoTracking()
            .OrderBy(row => row.RuleName).ThenBy(row => row.Sequence).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToRecord())];
    }

    public async Task<IReadOnlyList<StoredScenario>> ForRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Scenarios.AsNoTracking()
            .Where(row => row.RuleName == ruleName)
            .OrderBy(row => row.Sequence).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToRecord())];
    }

    public async Task<ScenarioWriteResult> PutAsync(StoredScenario scenario, int baseVersion, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Scenarios
            .SingleOrDefaultAsync(row => row.RuleName == scenario.RuleName && row.Id == scenario.Id, cancellationToken);
        var current = existing?.Version ?? 0;
        if (baseVersion != current)
            return ScenarioWriteResult.Conflict(current);

        var version = baseVersion + 1;
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            // Insertion order, kept as a column because SQLite cannot ORDER BY a DateTimeOffset and
            // a coarse clock would tie two quick adds anyway. A race between replicas can produce a
            // duplicate sequence; the id breaks the tie, and the order of two simultaneous adds on
            // two replicas was never meaningful.
            var sequence = (await context.Scenarios.MaxAsync(row => (long?)row.Sequence, cancellationToken) ?? 0) + 1;
            context.Scenarios.Add(new ScenarioRow
            {
                Sequence = sequence,
                RuleName = scenario.RuleName,
                Id = scenario.Id,
                Name = scenario.Name,
                ModelJson = scenario.ModelJson,
                ExpectedSatisfied = scenario.ExpectedSatisfied,
                SourceDecisionId = scenario.SourceDecisionId,
                Version = version,
                Author = scenario.Author,
                TimestampUtc = now,
            });
        }
        else
        {
            existing.Name = scenario.Name;
            existing.ModelJson = scenario.ModelJson;
            existing.ExpectedSatisfied = scenario.ExpectedSatisfied;
            existing.SourceDecisionId = scenario.SourceDecisionId;
            existing.Version = version;
            existing.Author = scenario.Author;
            existing.TimestampUtc = now;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ScenarioWriteResult.Written(version);
        }
        catch (DbUpdateException)
        {
            // Another replica moved the row between the read and this write: a concurrency-token
            // miss on update, or a key collision on create. Report where the row now stands.
            return ScenarioWriteResult.Conflict(await CurrentVersionAsync(scenario.RuleName, scenario.Id, cancellationToken));
        }
    }

    public async Task<ScenarioWriteResult> DeleteAsync(string ruleName, string id, int baseVersion, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Scenarios
            .SingleOrDefaultAsync(row => row.RuleName == ruleName && row.Id == id, cancellationToken);
        var current = existing?.Version ?? 0;
        if (existing is null || baseVersion != current)
            return ScenarioWriteResult.Conflict(current);

        context.Scenarios.Remove(existing);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ScenarioWriteResult.Written(current);
        }
        catch (DbUpdateException)
        {
            return ScenarioWriteResult.Conflict(await CurrentVersionAsync(ruleName, id, cancellationToken));
        }
    }

    private async Task<int> CurrentVersionAsync(string ruleName, string id, CancellationToken cancellationToken)
    {
        await using var fresh = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await fresh.Scenarios.AsNoTracking()
            .Where(row => row.RuleName == ruleName && row.Id == id)
            .Select(row => row.Version)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
