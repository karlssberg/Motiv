namespace Motiv.Tests.Diagnostics;

[Collection(TelemetryTestCollection.Name)]
public class AsyncEvaluationTelemetryTests
{
    [Fact]
    public async Task Should_emit_exactly_one_span_for_a_composed_async_proposition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");
        var isPositive = Spec.BuildAsync((int n) => Task.FromResult(n > 0)).Create("is positive");
        var composed = isEven.AndAlso(isPositive);

        await composed.EvaluateAsync(4);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.proposition").ShouldBe(composed.Description.Statement);
    }

    [Fact]
    public async Task Should_emit_exactly_one_span_when_a_sync_proposition_is_lifted_into_an_async_composition()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var isPositive = Spec.BuildAsync((int n) => Task.FromResult(n > 0)).Create("is positive");
        var composed = isPositive.And(isEven);

        await composed.EvaluateAsync(4);

        harness.Activities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_emit_the_same_telemetry_for_concurrent_and_sequential_composition()
    {
        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");
        var isPositive = Spec.BuildAsync((int n) => Task.FromResult(n > 0)).Create("is positive");

        using var sequentialHarness = new TelemetryHarness();
        await isEven.And(isPositive).EvaluateAsync(4);
        var sequential = sequentialHarness.SingleActivity();
        var sequentialTags = new
        {
            Satisfied = sequential.GetTagItem("motiv.satisfied"),
            Reason = sequential.GetTagItem("motiv.reason"),
            Assertions = sequential.GetTagItem("motiv.assertions")
        };
        sequentialHarness.Dispose();

        using var concurrentHarness = new TelemetryHarness();
        await isEven.AndConcurrently(isPositive).EvaluateAsync(4);
        var concurrent = concurrentHarness.SingleActivity();

        concurrent.GetTagItem("motiv.satisfied").ShouldBe(sequentialTags.Satisfied);
        concurrent.GetTagItem("motiv.reason").ShouldBe(sequentialTags.Reason);
        concurrent.GetTagItem("motiv.assertions").ShouldBe(sequentialTags.Assertions);
    }

    [Fact]
    public async Task Should_emit_one_span_per_async_policy_evaluation()
    {
        using var harness = new TelemetryHarness();

        var policy = Spec
            .BuildAsync((int n) => Task.FromResult(n % 2 == 0))
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("is even");

        await policy.EvaluateAsync(2);

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity().GetTagItem("motiv.satisfied").ShouldBe(true);
    }

    [Fact]
    public async Task Should_emit_nothing_for_MatchesAsync()
    {
        using var harness = new TelemetryHarness();

        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");

        (await isEven.MatchesAsync(2)).ShouldBeTrue();

        harness.Activities.ShouldBeEmpty();
        harness.Measurements.ShouldBeEmpty();
    }
}
