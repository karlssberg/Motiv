using System.Diagnostics;
using Motiv.Diagnostics;

namespace Motiv.Tests.Diagnostics;

// MotivTelemetry.ExplanationDetail is process-wide state. The Telemetry collection is serialized, and
// every test here restores the previous value in a finally so it cannot leak into another test.
[Collection(TelemetryTestCollection.Name)]
public class ExplanationDetailTelemetryTests
{
    [Fact]
    public void Full_is_the_default_and_tags_both_reason_and_assertions()
    {
        using var harness = new TelemetryHarness();

        MotivTelemetry.ExplanationDetail.ShouldBe(ExplanationDetail.Full);

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var result = isEven.Evaluate(3);

        var activity = harness.SingleActivity();
        activity.GetTagItem("motiv.reason").ShouldBe(result.Reason);
        activity.GetTagItem("motiv.assertions").ShouldBe(result.Assertions.ToArray());
    }

    [Fact]
    public void ReasonOnly_tags_reason_and_omits_assertions()
    {
        using var harness = new TelemetryHarness();
        var previous = MotivTelemetry.ExplanationDetail;
        MotivTelemetry.ExplanationDetail = ExplanationDetail.ReasonOnly;
        try
        {
            var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
            var result = isEven.Evaluate(3);

            var activity = harness.SingleActivity();
            activity.GetTagItem("motiv.reason").ShouldBe(result.Reason);
            activity.GetTagItem("motiv.assertions").ShouldBeNull();
        }
        finally
        {
            MotivTelemetry.ExplanationDetail = previous;
        }
    }

    [Fact]
    public void None_omits_both_reason_and_assertions_but_keeps_the_outcome()
    {
        using var harness = new TelemetryHarness();
        var previous = MotivTelemetry.ExplanationDetail;
        MotivTelemetry.ExplanationDetail = ExplanationDetail.None;
        try
        {
            var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
            var result = isEven.Evaluate(3);

            var activity = harness.SingleActivity();
            activity.GetTagItem("motiv.satisfied").ShouldBe(result.Satisfied);
            activity.GetTagItem("motiv.reason").ShouldBeNull();
            activity.GetTagItem("motiv.assertions").ShouldBeNull();
        }
        finally
        {
            MotivTelemetry.ExplanationDetail = previous;
        }
    }
}
