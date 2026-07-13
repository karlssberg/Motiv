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

        object? sequentialSatisfied;
        object? sequentialReason;
        object? sequentialAssertions;
        using (var sequentialHarness = new TelemetryHarness())
        {
            await isEven.And(isPositive).EvaluateAsync(4);
            var sequential = sequentialHarness.SingleActivity();
            sequentialSatisfied = sequential.GetTagItem("motiv.satisfied");
            sequentialReason = sequential.GetTagItem("motiv.reason");
            sequentialAssertions = sequential.GetTagItem("motiv.assertions");
        }

        var sequentialTags = new
        {
            Satisfied = sequentialSatisfied,
            Reason = sequentialReason,
            Assertions = sequentialAssertions
        };

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

    [Fact]
    public async Task Should_emit_exactly_one_span_for_an_async_OrElse_policy_when_the_left_is_satisfied()
    {
        using var harness = new TelemetryHarness();

        var left = Spec.BuildAsync((int _) => Task.FromResult(true)).WhenTrue(1).WhenFalse(0).Create("left");
        var right = Spec.BuildAsync((int _) => Task.FromResult(true)).WhenTrue(2).WhenFalse(0).Create("right");
        var composed = left.OrElse(right);

        var result = await composed.EvaluateAsync(0);

        harness.Activities.Count.ShouldBe(1);
        result.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_emit_exactly_one_span_for_an_async_OrElse_policy_when_the_left_is_unsatisfied()
    {
        using var harness = new TelemetryHarness();

        var left = Spec.BuildAsync((int _) => Task.FromResult(false)).WhenTrue(1).WhenFalse(0).Create("left");
        var right = Spec.BuildAsync((int _) => Task.FromResult(true)).WhenTrue(2).WhenFalse(0).Create("right");
        var composed = left.OrElse(right);

        var result = await composed.EvaluateAsync(0);

        harness.Activities.Count.ShouldBe(1);
        result.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_emit_exactly_one_span_for_an_async_policy_negation()
    {
        using var harness = new TelemetryHarness();

        var policy = Spec
            .BuildAsync((int n) => Task.FromResult(n % 2 == 0))
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("is even");
        var negated = policy.Not();

        var result = await negated.EvaluateAsync(2);

        harness.Activities.Count.ShouldBe(1);
        result.Satisfied.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_emit_exactly_one_span_when_composing_async_specs_with_differing_metadata_types()
    {
        using var harness = new TelemetryHarness();

        AsyncSpecBase<int> isEven = Spec
            .BuildAsync((int n) => Task.FromResult(n % 2 == 0))
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("is even");
        AsyncSpecBase<int> isPositive = Spec
            .BuildAsync((int n) => Task.FromResult(n > 0))
            .WhenTrue(Guid.NewGuid())
            .WhenFalse(Guid.Empty)
            .Create("is positive");

        var composed = isEven & isPositive;

        await composed.EvaluateAsync(4);

        harness.Activities.Count.ShouldBe(1);
    }

    private sealed class IsPositiveAsync() : AsyncSpec<int>(
        Spec.BuildAsync((int n) => Task.FromResult(n > 0))
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create());

    private sealed class GradeAsync() : AsyncSpec<int, char>(() =>
        Spec.BuildAsync((int n) => Task.FromResult(n >= 50))
            .WhenTrue('P')
            .WhenFalse('F')
            .Create("passing grade"));

    [Fact]
    public async Task Should_emit_exactly_one_span_for_an_AsyncSpec_TModel_wrapper()
    {
        using var harness = new TelemetryHarness();

        var spec = new IsPositiveAsync();

        await spec.EvaluateAsync(1);

        harness.Activities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_emit_exactly_one_span_for_an_AsyncSpec_TModel_TMetadata_wrapper()
    {
        using var harness = new TelemetryHarness();

        var spec = new GradeAsync();

        await spec.EvaluateAsync(80);

        harness.Activities.Count.ShouldBe(1);
    }
}
