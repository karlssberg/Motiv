namespace Motiv.Serialization;

/// <summary>
/// Where a rule's scenarios are kept. Rows are replaced in place under a per-row compare-and-set;
/// there is no version log, because a scenario is test data and a superseded sample is not a replay
/// anchor. Scoped by rule: an id is unique within its rule only.
/// </summary>
public interface IScenarioStore
{
    /// <summary>Every scenario of every rule.</summary>
    Task<IReadOnlyList<StoredScenario>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>The scenarios of one rule, in the order they were added.</summary>
    Task<IReadOnlyList<StoredScenario>> ForRuleAsync(string ruleName, CancellationToken cancellationToken);

    /// <summary>
    /// Creates (<paramref name="baseVersion"/> 0, refused when the id exists) or replaces
    /// (<paramref name="baseVersion"/> must equal the stored version) one scenario. The row's
    /// <see cref="StoredScenario.Version"/> is ignored; the written version is <c>baseVersion + 1</c>
    /// and the timestamp is the store's.
    /// </summary>
    Task<ScenarioWriteResult> PutAsync(StoredScenario scenario, int baseVersion, CancellationToken cancellationToken);

    /// <summary>Removes one scenario, when <paramref name="baseVersion"/> is the stored version.</summary>
    Task<ScenarioWriteResult> DeleteAsync(string ruleName, string id, int baseVersion, CancellationToken cancellationToken);
}

/// <summary>The default store: scenarios live for the lifetime of the process.</summary>
public sealed class InMemoryScenarioStore : IScenarioStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Rule, string Id), StoredScenario> _rows = new();
    private readonly List<(string Rule, string Id)> _order = [];

    public Task<IReadOnlyList<StoredScenario>> LoadAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<StoredScenario>>([.. _order.Select(key => _rows[key])]);
    }

    public Task<IReadOnlyList<StoredScenario>> ForRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredScenario>>(
                [.. _order.Where(key => key.Rule == ruleName).Select(key => _rows[key])]);
        }
    }

    public Task<ScenarioWriteResult> PutAsync(StoredScenario scenario, int baseVersion, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (scenario.RuleName, scenario.Id);
            var current = _rows.TryGetValue(key, out var existing) ? existing.Version : 0;
            if (baseVersion != current)
                return Task.FromResult(ScenarioWriteResult.Conflict(current));

            if (existing is null)
                _order.Add(key);

            var version = baseVersion + 1;
            _rows[key] = scenario with { Version = version, TimestampUtc = DateTimeOffset.UtcNow };
            return Task.FromResult(ScenarioWriteResult.Written(version));
        }
    }

    public Task<ScenarioWriteResult> DeleteAsync(string ruleName, string id, int baseVersion, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (ruleName, id);
            var current = _rows.TryGetValue(key, out var existing) ? existing.Version : 0;
            if (existing is null || baseVersion != current)
                return Task.FromResult(ScenarioWriteResult.Conflict(current));

            _rows.Remove(key);
            _order.Remove(key);
            return Task.FromResult(ScenarioWriteResult.Written(current));
        }
    }
}
