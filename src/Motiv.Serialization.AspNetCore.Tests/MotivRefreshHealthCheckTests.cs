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
}
