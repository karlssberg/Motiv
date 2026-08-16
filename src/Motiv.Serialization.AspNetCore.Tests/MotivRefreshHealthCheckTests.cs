using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Motiv.Serialization.AspNetCore.Tests;

public class MotivRefreshHealthCheckTests
{
    /// <summary>
    /// Builds a <see cref="MotivRefreshService"/> that has never ticked and stamps
    /// <paramref name="report"/> straight onto its internal <c>LastReport</c> setter — the health
    /// check only reads that property, so driving a real poll loop (and a store, and a rule) to
    /// produce a report would be exercising <c>MotivRefreshServiceTests</c>' job a second time here.
    /// </summary>
    /// <param name="report">The outcome to report, or omitted for a replica that has not polled yet.</param>
    private static MotivRefreshService ServiceReporting(RefreshReport? report = null) =>
        new(new RuleSet(new SpecRegistry()), new MotivRefreshOptions(),
            NullLogger<MotivRefreshService>.Instance)
        {
            LastReport = report
        };

    [Fact]
    public async Task Should_report_healthy_before_the_first_poll()
    {
        // Arrange
        var check = new MotivRefreshHealthCheck(ServiceReporting());

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert — LastReport is null until the first tick; that is "not yet polled", not "diverged"
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Should_report_healthy_when_the_replica_is_converged()
    {
        // Arrange
        var check = new MotivRefreshHealthCheck(ServiceReporting(
            RefreshReport.Applied(new StoreGeneration(4, 1), [])));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data["generation"].ShouldBe("r4.p1");
        result.Data["outcome"].ShouldBe(nameof(RefreshOutcome.Applied));
        result.Data["quarantined"].ShouldBe(0);
    }

    [Fact]
    public async Task Should_surface_quarantined_debt_carried_forward_on_a_successful_apply()
    {
        // Arrange — Applied does not mean nothing is wrong: a quarantined row has no live binding to
        // protect, so it does not block the rebuild, but it is carried forward still broken. The
        // poller's own log line already reports this count on Applied, so the health endpoint must
        // not be less informative than the logs about the same event.
        var check = new MotivRefreshHealthCheck(ServiceReporting(
            RefreshReport.Applied(
                new StoreGeneration(4, 1),
                [new RefreshFailure("stale-rule", "rule", [])])));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert — still healthy (quarantine never blocks convergence), but the debt is visible
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data["quarantined"].ShouldBe(1);
        result.Description.ShouldNotBeNull().ShouldContain("stale-rule");
    }

    [Fact]
    public async Task Should_report_healthy_when_a_publish_won_the_race()
    {
        // Arrange — Contended is not a failure: a publish landed mid-rebuild and won the swap, so
        // the replica is on a world at least as new as the one it was building, and the next tick
        // proceeds on its own.
        var check = new MotivRefreshHealthCheck(ServiceReporting(
            RefreshReport.Contended(new StoreGeneration(4, 1))));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Should_report_degraded_when_the_last_refresh_aborted()
    {
        // Arrange
        var check = new MotivRefreshHealthCheck(ServiceReporting(
            RefreshReport.Aborted(
                new StoreGeneration(4, 1),
                [new RefreshFailure("number", "rule", [])],
                [])));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert — degraded, not unhealthy: the replica is serving correctly, just not the newest
        // world, and taking it out of rotation would turn a stale pod into a missing pod.
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull().ShouldContain("number");
        result.Data["generation"].ShouldBe("r4.p1");
        result.Data["outcome"].ShouldBe(nameof(RefreshOutcome.Aborted));
        result.Data["quarantined"].ShouldBe(0);
    }

    [Fact]
    public async Task Should_name_every_regression_the_operator_needs_to_act_on()
    {
        // Arrange — more than one blocked node: the description is the diagnostic payload, so it
        // must not silently report only the first one.
        var check = new MotivRefreshHealthCheck(ServiceReporting(
            RefreshReport.Aborted(
                new StoreGeneration(4, 1),
                [new RefreshFailure("number", "rule", []), new RefreshFailure("is-adult", "proposition", [])],
                [])));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        var description = result.Description.ShouldNotBeNull();
        description.ShouldContain("number");
        description.ShouldContain("is-adult");
    }

    [Fact]
    public async Task Should_surface_quarantined_alongside_regressions_when_aborted()
    {
        // Arrange — regressions and quarantined rows come from the same pass and may well be
        // related: the same redeploy that made one document unbindable is a plausible cause of the
        // other. An operator diagnosing a stall wants both halves in one place, which is the whole
        // reason RefreshReport.Aborted carries quarantined alongside regressions in the first place.
        var check = new MotivRefreshHealthCheck(ServiceReporting(
            RefreshReport.Aborted(
                new StoreGeneration(4, 1),
                [new RefreshFailure("number", "rule", [])],
                [new RefreshFailure("stale-rule", "rule", [])])));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert — both the blocking cause and the pre-existing debt are visible
        result.Data["quarantined"].ShouldBe(1);
        var description = result.Description.ShouldNotBeNull();
        description.ShouldContain("number");
        description.ShouldContain("stale-rule");
    }

    [Fact]
    public async Task Should_report_only_the_quarantined_count_when_the_list_is_long()
    {
        // Arrange — six quarantined rows, one past MaxNamedInDescription: naming all of them would
        // turn a one-line diagnostic hint into a catalog dump. The count still lands in Data either
        // way — only the description's name-list is proportionate.
        var quarantined = Enumerable.Range(1, 6)
            .Select(i => new RefreshFailure($"stale-{i}", "rule", []))
            .ToArray();
        var check = new MotivRefreshHealthCheck(ServiceReporting(
            RefreshReport.Applied(new StoreGeneration(4, 1), quarantined)));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        result.Data["quarantined"].ShouldBe(6);
        var description = result.Description.ShouldNotBeNull();
        description.ShouldContain("6");
        description.ShouldNotContain("stale-1");
    }
}
