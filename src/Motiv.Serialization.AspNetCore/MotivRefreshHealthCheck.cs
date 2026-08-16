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
/// <para>
/// <see cref="RefreshReport.Quarantined"/> is surfaced on every branch, not only
/// <see cref="RefreshOutcome.Aborted"/>: an <see cref="RefreshOutcome.Applied"/> refresh can carry
/// quarantined debt forward silently (by design — see <see cref="RefreshReport"/>'s own remarks),
/// and the poller's log line already reports that count on <see cref="RefreshOutcome.Applied"/>, so
/// a health endpoint that stayed silent about it would be strictly less informative than the logs
/// about the very event it exists to surface instead of. On <see cref="RefreshOutcome.Aborted"/> it
/// matters even more: <see cref="RefreshReport.Regressions"/> and <see cref="RefreshReport
/// .Quarantined"/> come from the same pass, and a row already quarantined is a plausible cause of a
/// row that just regressed — an operator diagnosing a stall wants both halves in one place, which is
/// the whole reason <see cref="RefreshReport.Aborted"/> carries <c>quarantined</c> alongside
/// <c>regressions</c> in the first place. The count always lands in <see cref="HealthCheckResult
/// .Data"/>; the names join it in <see cref="HealthCheckResult.Description"/> only when the list is
/// short enough to stay a diagnostic hint rather than turn the one-liner into the full catalog dump
/// — past <see cref="MaxNamedInDescription"/>, the count alone is enough to send an operator to the
/// catalog endpoint for the rest.
/// </para>
/// </remarks>
internal sealed class MotivRefreshHealthCheck(MotivRefreshService service) : IHealthCheck
{
    /// <summary>
    /// How many quarantined names <see cref="Check"/> will list by name in the description before
    /// falling back to the count alone.
    /// </summary>
    private const int MaxNamedInDescription = 5;

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
            ["outcome"] = report.Outcome.ToString(),
            ["quarantined"] = report.Quarantined.Count
        };

        var quarantinedNote = DescribeQuarantined(report.Quarantined);

        if (report.Outcome is not RefreshOutcome.Aborted)
            return HealthCheckResult.Healthy($"Motiv is on generation {generation}.{quarantinedNote}", data: data);

        var names = string.Join(", ", report.Regressions.Select(failure => failure.Name));
        return HealthCheckResult.Degraded(
            $"Motiv is stuck on generation {generation}: {names} would regress a live binding.{quarantinedNote}",
            data: data);
    }

    /// <summary>
    /// A sentence fragment (leading space included, empty when there is nothing to report) naming
    /// what is still quarantined — proportionate to the list's size, per this class's remarks.
    /// </summary>
    private static string DescribeQuarantined(IReadOnlyList<RefreshFailure> quarantined)
    {
        if (quarantined.Count == 0)
            return string.Empty;

        var names = quarantined.Count <= MaxNamedInDescription
            ? ": " + string.Join(", ", quarantined.Select(failure => failure.Name))
            : string.Empty;

        return $" {quarantined.Count} document(s) still quarantined{names}.";
    }
}
