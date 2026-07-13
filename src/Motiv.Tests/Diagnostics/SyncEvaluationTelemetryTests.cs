using System.Diagnostics;

namespace Motiv.Tests.Diagnostics;

[Collection(TelemetryTestCollection.Name)]
public class SyncEvaluationTelemetryTests
{
    [Fact]
    public void Should_emit_exactly_one_span_for_a_deeply_composed_proposition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var isPositive = Spec.Build((int n) => n > 0).Create("is positive");
        var isSmall = Spec.Build((int n) => n < 100).Create("is small");
        var composed = (isEven & isPositive).AndAlso(!isSmall | isEven);

        composed.Evaluate(4);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.proposition").ShouldBe(composed.Description.Statement);
    }

    [Fact]
    public void Should_emit_exactly_one_span_for_a_higher_order_proposition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var allEven = Spec.Build(isEven).AsAllSatisfied().Create("all even");

        allEven.Evaluate([2, 4, 6]);

        harness.Activities.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_tag_the_span_with_the_results_own_explanation()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var result = isEven.Evaluate(3);

        var activity = harness.SingleActivity();
        activity.GetTagItem("motiv.satisfied").ShouldBe(result.Satisfied);
        activity.GetTagItem("motiv.reason").ShouldBe(result.Reason);
        activity.GetTagItem("motiv.assertions").ShouldBe(result.Assertions.ToArray());
    }

    [Fact]
    public void Should_emit_one_span_per_policy_evaluation()
    {
        using var harness = new TelemetryHarness();

        var policy = Spec
            .Build((int n) => n % 2 == 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("is even");

        policy.Evaluate(2);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.satisfied").ShouldBe(true);
    }

    [Fact]
    public void Should_emit_nothing_for_Matches()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var composed = isEven & Spec.Build((int n) => n > 0).Create("is positive");

        composed.Matches(2).ShouldBeTrue();

        harness.Activities.ShouldBeEmpty();
        harness.Measurements.ShouldBeEmpty();
    }

    [Fact]
    public void Should_record_both_instruments_once_per_evaluation()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        isEven.Evaluate(2);
        isEven.Evaluate(3);

        harness.Measurements
            .Count(measurement => measurement.Instrument == "motiv.evaluations")
            .ShouldBe(2);
        harness.Measurements
            .Count(measurement => measurement.Instrument == "motiv.evaluation.duration")
            .ShouldBe(2);
    }

    [Fact]
    public void Should_emit_one_span_per_model_when_filtering_a_collection()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        _ = new[] { 1, 2, 3 }.Where(isEven).ToList();

        harness.Activities.Count.ShouldBe(3);
    }

    [Fact]
    public void Should_emit_exactly_one_span_when_composing_specs_with_differing_metadata_types()
    {
        using var harness = new TelemetryHarness();

        SpecBase<int> isEven = Spec
            .Build((int n) => n % 2 == 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("is even");
        SpecBase<int> isPositive = Spec
            .Build((int n) => n > 0)
            .WhenTrue(Guid.NewGuid())
            .WhenFalse(Guid.Empty)
            .Create("is positive");

        var composed = isEven & isPositive;

        composed.Evaluate(4);

        harness.Activities.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_emit_exactly_one_span_for_an_expression_tree_proposition()
    {
        using var harness = new TelemetryHarness();
        var isValid = Spec
            .From((int n) => n > 0 && n % 2 == 0)
            .WhenTrue("valid")
            .WhenFalse("invalid")
            .Create("is valid");

        isValid.Evaluate(4);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.proposition").ShouldBe(isValid.Description.Statement);
    }
}
