using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Readiness: whether this replica's stores answer at all, asked by reading each one's generation.
/// </summary>
/// <remarks>
/// <para>
/// The generation read is the right probe because it is the cheapest thing a store can be asked that
/// still proves the connection works — one scalar, no rows, the same call the refresh poller already
/// makes on a timer. Probing with a row read would make readiness proportional to catalog size, and
/// probing with a synthetic write would make a health check a writer.
/// </para>
/// <para>
/// <strong>Distinct from <see cref="MotivRefreshHealthCheck"/>, and deliberately harsher.</strong>
/// That one asks whether this replica has <em>converged</em>, and reports
/// <see cref="HealthStatus.Degraded"/> when it has not, because a replica serving an older approved
/// world is still serving correctly. This one asks whether the store answers, and reports
/// <see cref="HealthStatus.Unhealthy"/> when it does not: a replica that cannot reach its store can
/// neither publish nor converge, and will not recover by being sent more traffic.
/// </para>
/// <para>
/// Both stores are probed when the host has both. They are never written in the same transaction and
/// may well be different databases, so one answering says nothing about the other.
/// </para>
/// </remarks>
/// <param name="rules">The rule set, whose store is always probed.</param>
/// <param name="propositions">The proposition set, or null in a host that mounts no propositions.</param>
internal sealed class MotivStoreHealthCheck(RuleSet rules, PropositionSet? propositions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>(StringComparer.Ordinal);

        // Probed in turn rather than concurrently: a failure names which store failed, and two
        // scalar reads at probe cadence are not worth the ambiguity of an AggregateException.
        if (await ProbeAsync(
                "rule store", data, "rules.generation",
                () => rules.StoreGenerationAsync(cancellationToken)).ConfigureAwait(false)
            is { } ruleFailure)
        {
            return ruleFailure;
        }

        if (propositions is not null
            && await ProbeAsync(
                "proposition store", data, "propositions.generation",
                () => propositions.StoreGenerationAsync(cancellationToken)).ConfigureAwait(false)
            is { } propositionFailure)
        {
            return propositionFailure;
        }

        return HealthCheckResult.Healthy("Motiv's stores are answering.", data);
    }

    /// <summary>
    /// Reads one store's generation, recording it under <paramref name="key"/>.
    /// </summary>
    /// <returns>Null when the store answered, or the unhealthy result to report when it did not.</returns>
    private static async Task<HealthCheckResult?> ProbeAsync(
        string store, Dictionary<string, object> data, string key, Func<Task<long>> read)
    {
        try
        {
            data[key] = await read().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            // Every exception, cancellation included. A probe the health endpoint cancelled because
            // it took too long is a store that did not answer in time, which is exactly the state
            // readiness exists to report — letting OperationCanceledException through instead would
            // surface a slow database as an unhandled fault in the health pipeline.
            return HealthCheckResult.Unhealthy(
                $"Motiv's {store} did not answer.", exception, data);
        }
    }
}
