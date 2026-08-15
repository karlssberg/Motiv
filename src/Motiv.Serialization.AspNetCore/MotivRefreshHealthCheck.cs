using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Whether this replica is converging. Reports <see cref="HealthStatus.Degraded"/> — not
/// <see cref="HealthStatus.Unhealthy"/> — when the last refresh aborted: the replica is serving a
/// coherent, approved world correctly, it just is not the newest one, and taking it out of load
/// balancer rotation over that would turn a stale pod into a missing pod, which is strictly worse.
/// </summary>
/// <remarks>
/// <para>
/// Depends on <see cref="MotivRefreshService"/> directly, rather than an abstraction, so that
/// <see cref="MotivRulesBuilder.AddRefresh"/>'s registration of the service as a singleton — and
/// resolution of the hosted service from that same singleton — guarantees this reads the exact
/// instance the poll loop writes.
/// </para>
/// <para>
/// <see cref="RefreshOutcome.Contended"/> is not a failure: a publish landed mid-rebuild and won the
/// swap, so the replica is already on a world at least as new as the one it was building, and the
/// next tick proceeds — it is reported healthy, the same as <see cref="RefreshOutcome.Applied"/> and
/// <see cref="RefreshOutcome.Unchanged"/>. This is deliberately weaker than
/// <see cref="RefreshReport.IsConverged"/>, which excludes <see cref="RefreshOutcome.Contended"/>:
/// a caller polling for convergence should retry, but a replica that will resolve itself on the next
/// tick is not something to page anyone about.
/// </para>
/// </remarks>
internal sealed class MotivRefreshHealthCheck(MotivRefreshService service) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(Check(service.LastReport));

    /// <param name="report">The last refresh outcome, or null when the poller has not ticked yet.</param>
    private static HealthCheckResult Check(RefreshReport? report)
    {
        // Null until the first tick — not yet converged or diverged, just not yet polled. Healthy
        // rather than degraded: a freshly started replica is serving its compiled/loaded defaults
        // correctly, and reporting it degraded before the poller has even had a chance to run would
        // be a false alarm on every cold start.
        if (report is null)
            return HealthCheckResult.Healthy("Motiv has not polled yet.");

        var generation = report.Generation.ToToken();
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["generation"] = generation,
            ["outcome"] = report.Outcome.ToString()
        };

        if (report.Outcome is not RefreshOutcome.Aborted)
            return HealthCheckResult.Healthy($"Motiv is on generation {generation}.", data: data);

        var names = string.Join(", ", report.Regressions.Select(failure => failure.Name));
        return HealthCheckResult.Degraded(
            $"Motiv is stuck on generation {generation}: {names} would regress a live binding.", data: data);
    }
}
