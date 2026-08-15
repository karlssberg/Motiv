using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Polls the stores' generation and rebuilds this replica when it moves. Opt-in: a single-replica
/// host does not need it, and starting a timer nobody asked for is not a default worth having.
/// </summary>
/// <remarks>
/// The loop never throws. A store outage, a cancelled rebuild, or a rebuild that lost its swap are
/// all ordinary outcomes of a background poller, and taking the host down over any of them would
/// trade a stale replica for no replica.
/// </remarks>
internal sealed class MotivRefreshService(
    RuleSet rules, MotivRefreshOptions options, ILogger<MotivRefreshService> logger)
    : BackgroundService
{
    private RefreshReport? _lastReport;

    /// <summary>
    /// The most recent outcome, for the health check to report. Null until the first tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by this service's own loop, read from whatever thread handles the health-check
    /// request — a plain auto-property gives no guarantee the reader ever observes a write from a
    /// different thread. <see cref="Volatile.Read{T}"/>/<see cref="Volatile.Write{T}"/> is this
    /// codebase's convention for exactly that cross-thread pairing — see
    /// <c>BindingScope.Current</c> and <c>ChangeRequest.Status</c> — so this follows it too.
    /// </para>
    /// <para>
    /// The setter is internal, not private: the health check tests construct a
    /// <see cref="MotivRefreshService"/> directly and stamp a <see cref="RefreshReport"/> onto it
    /// without driving a real tick, via this assembly's <c>InternalsVisibleTo</c> for its test
    /// assembly. Only this class's own loop uses it in production.
    /// </para>
    /// </remarks>
    public RefreshReport? LastReport
    {
        get => Volatile.Read(ref _lastReport);
        internal set => Volatile.Write(ref _lastReport, value);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Interval, stoppingToken).ConfigureAwait(false);
                var report = await rules.RefreshAsync(stoppingToken).ConfigureAwait(false);
                LastReport = report;
                LogOutcome(report);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Motiv refresh failed; keeping the current world and retrying.");
            }
        }
    }

    /// <summary>
    /// Logs the two outcomes an operator needs to hear about. <see cref="RefreshOutcome.Unchanged"/>
    /// is the common case every tick and <see cref="RefreshOutcome.Contended"/> resolves itself on the
    /// next one, so neither is worth a line per interval for the lifetime of the process.
    /// </summary>
    private void LogOutcome(RefreshReport report)
    {
        switch (report.Outcome)
        {
            case RefreshOutcome.Applied:
                logger.LogInformation(
                    "Motiv rebuilt on generation {Generation}; {Quarantined} stored document(s) carried quarantined.",
                    report.Generation.ToToken(), report.Quarantined.Count);
                break;

            // Loud, and at Error: this replica is knowingly serving an older world, and will keep
            // doing so until the store or this build changes.
            case RefreshOutcome.Aborted:
                logger.LogError(
                    "Motiv refresh aborted: {Count} stored document(s) would regress a live binding, " +
                    "so generation {Generation} is still being served. First: {Name}.",
                    report.Regressions.Count, report.Generation.ToToken(), report.Regressions[0].Name);
                break;
        }
    }
}
