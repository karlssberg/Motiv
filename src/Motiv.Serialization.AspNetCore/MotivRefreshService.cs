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

    // Test seam, capped at one permit: see WaitForTickAsync's doc for why a cap of one is enough
    // and why nothing here can overflow or fault the loop.
    private readonly SemaphoreSlim _tickSignal = new(initialCount: 0, maxCount: 1);

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

    /// <summary>
    /// Test seam: completes once a poll cycle — success or a caught failure, whatever
    /// <see cref="LogOutcome"/> or the catch-all below just handled — has finished since the last
    /// time this was awaited (or since startup, if this has never been awaited before). Reached
    /// through this assembly's <c>InternalsVisibleTo</c>, the same way <see cref="LastReport"/>'s
    /// setter is; nothing in production ever awaits it, only this class's own loop signals it.
    /// </summary>
    /// <remarks>
    /// A tick that completes before a caller ever awaits this is not lost. The signal is a
    /// <see cref="SemaphoreSlim"/> capped at one pending permit: a completed tick releases it if it
    /// is not already held, and awaiting later still consumes whatever is pending. One permit is
    /// enough because a caller only ever needs to know "has at least one more tick happened since I
    /// last checked" — it re-reads <see cref="LastReport"/> (or whatever else it is polling for)
    /// itself after waking, rather than trusting the permit count to carry state. This is what makes
    /// it safe to await *after* attaching late: a naive single-shot signal (e.g. a
    /// <see cref="TaskCompletionSource"/> replaced per tick) can be missed entirely if a waiter
    /// attaches between one tick completing and the next one starting, reintroducing the exact
    /// wall-clock race this exists to remove. It also means nothing here can overflow or fault the
    /// loop even if a test never drains it: releasing an already-full semaphore would normally throw
    /// <see cref="SemaphoreFullException"/>, so the loop checks first rather than relying on a
    /// try/catch, and the check-then-release has a single producer — this loop — so it cannot race
    /// with itself.
    /// </remarks>
    /// <param name="cancellationToken">
    /// Cancels the wait. Callers should bound this — e.g. from a test-side timeout — so a service
    /// that has stopped ticking entirely (a real hang) surfaces as a failed wait rather than one that
    /// never returns; a healthy run never approaches the bound, since progress is driven by real
    /// ticks, not by how much of the bound has elapsed.
    /// </param>
    internal Task WaitForTickAsync(CancellationToken cancellationToken = default) =>
        _tickSignal.WaitAsync(cancellationToken);

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

            SignalTick();
        }
    }

    /// <summary>
    /// Wakes anything awaiting <see cref="WaitForTickAsync"/>. See that method's doc for why a
    /// single pending permit is sufficient and why this check-then-release cannot race with itself.
    /// </summary>
    private void SignalTick()
    {
        if (_tickSignal.CurrentCount == 0)
            _tickSignal.Release();
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
