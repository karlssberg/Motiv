namespace Motiv.Tests.Diagnostics;

/// <summary>
/// Covers the metrics-only branch — a caller who wants counters/duration but never subscribed an
/// <c>ActivityListener</c>. In that branch <c>EvaluationScope.Start</c> never opens an activity, so
/// <c>Complete</c>'s <c>activity is not null</c> guard is never entered: <c>Reason</c>/<c>Assertions</c> must
/// never be resolved, and any WhenTrue/WhenFalse delegate backing them must never run.
/// </summary>
[Collection(TelemetryTestCollection.Name)]
public class MetricsOnlyTelemetryTests
{
    [Fact]
    public void Should_record_metrics_without_resolving_the_explanation_when_only_a_meter_listener_is_attached()
    {
        using var harness = new MetricsOnlyTelemetryHarness();

        var explanationDelegateRan = false;
        var proposition = Spec
            .Build((int _) => true)
            .WhenTrue(_ =>
            {
                explanationDelegateRan = true;
                return "value";
            })
            .WhenFalse("not value")
            .Create("has value");

        var result = proposition.Evaluate(1);

        result.Satisfied.ShouldBeTrue();
        explanationDelegateRan.ShouldBeFalse();

        harness.SingleMeasurement("motiv.evaluations").Tags["motiv.satisfied"].ShouldBe(true);
    }
}
