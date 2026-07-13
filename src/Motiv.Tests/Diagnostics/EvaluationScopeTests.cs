using System.Diagnostics;
using Motiv.Diagnostics;

namespace Motiv.Tests.Diagnostics;

public class EvaluationScopeTests
{
    [Fact]
    public void Should_emit_no_activity_when_nothing_is_listening()
    {
        var result = Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(2);

        var scope = EvaluationScope.Start("is even");
        scope.Complete(result);

        Activity.Current.ShouldBeNull();
        MotivTelemetry.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Should_report_enabled_when_a_listener_is_attached()
    {
        using var harness = new TelemetryHarness();

        MotivTelemetry.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Should_tag_a_completed_scope_with_the_results_own_explanation()
    {
        // The result is produced BEFORE the harness exists: from Task 2 onwards Evaluate is itself
        // instrumented, and an evaluation inside the harness would add a second, unrelated activity.
        var result = Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(3);

        using var harness = new TelemetryHarness();

        var scope = EvaluationScope.Start("is even");
        scope.Complete(result);

        var activity = harness.SingleActivity();
        activity.OperationName.ShouldBe("motiv.evaluate");
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.GetTagItem("motiv.proposition").ShouldBe("is even");
        activity.GetTagItem("motiv.satisfied").ShouldBe(false);
        activity.GetTagItem("motiv.reason").ShouldBe("is even == false");
        activity.GetTagItem("motiv.assertions").ShouldBe(new[] { "is even == false" });
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public void Should_record_both_instruments_on_a_completed_scope()
    {
        // Produced before the harness — see the note above.
        var result = Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(2);

        using var harness = new TelemetryHarness();

        var scope = EvaluationScope.Start("is even");
        scope.Complete(result);

        var count = harness.SingleMeasurement("motiv.evaluations");
        count.Value.ShouldBe(1);
        count.Tags["motiv.proposition"].ShouldBe("is even");
        count.Tags["motiv.satisfied"].ShouldBe(true);
        count.Tags.ShouldNotContainKey("error.type");

        var duration = harness.SingleMeasurement("motiv.evaluation.duration");
        duration.Value.ShouldBeGreaterThanOrEqualTo(0);
        duration.Tags["motiv.proposition"].ShouldBe("is even");
    }

    [Fact]
    public void Should_mark_a_failed_scope_with_the_error_type()
    {
        using var harness = new TelemetryHarness();

        var scope = EvaluationScope.Start("is even");
        scope.Fail(new InvalidOperationException("boom"));

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        activity.Events.ShouldContain(activityEvent => activityEvent.Name == "exception");
        activity.GetTagItem("motiv.satisfied").ShouldBeNull();

        var count = harness.SingleMeasurement("motiv.evaluations");
        count.Tags["error.type"].ShouldBe("System.InvalidOperationException");
        count.Tags.ShouldNotContainKey("motiv.satisfied");
    }
}
