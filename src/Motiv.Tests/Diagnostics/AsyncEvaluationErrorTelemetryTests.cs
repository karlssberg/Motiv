using System.Diagnostics;

namespace Motiv.Tests.Diagnostics;

/// <summary>
/// Covers error and cancellation semantics at the async evaluation boundary. The sync boundary is covered by
/// <see cref="EvaluationErrorTelemetryTests" />; these tests exist because the async boundary's exception and
/// cancellation paths otherwise have no boundary-level coverage — <c>EvaluateSpecInstrumentedAsync</c> and
/// <c>EvaluatePolicyInstrumentedAsync</c> (see <c>AsyncSpecBase{TModel,TMetadata}</c> and
/// <c>AsyncPolicyBase{TModel,TMetadata}</c>) call <c>scope.Fail</c> then a bare <c>throw;</c>, exactly like the
/// sync boundary, so the same guarantees must hold there too.
/// </summary>
[Collection(TelemetryTestCollection.Name)]
public class AsyncEvaluationErrorTelemetryTests
{
    [Fact]
    public async Task Should_mark_the_span_as_errored_and_rethrow_the_original_exception_unwrapped()
    {
        using var harness = new TelemetryHarness();
        var original = new InvalidOperationException("async boom");

        var throws = Spec.BuildAsync((int _) => throw original).Create("throws");

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => throws.EvaluateAsync(1));
        exception.Message.ShouldBe("async boom");
        ReferenceEquals(exception, original).ShouldBeTrue();

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        activity.GetTagItem("motiv.satisfied").ShouldBeNull();
    }

    [Fact]
    public async Task Should_record_the_error_type_on_both_instruments_for_an_async_failure()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .BuildAsync((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");

        await Should.ThrowAsync<InvalidOperationException>(() => throws.EvaluateAsync(1));

        harness.SingleMeasurement("motiv.evaluations")
            .Tags["error.type"].ShouldBe("System.InvalidOperationException");
        harness.SingleMeasurement("motiv.evaluation.duration")
            .Tags["error.type"].ShouldBe("System.InvalidOperationException");
    }

    [Fact]
    public async Task Should_attribute_an_async_composed_failure_to_the_root_proposition()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .BuildAsync((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");
        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");
        var composed = isEven.And(throws);

        await Should.ThrowAsync<InvalidOperationException>(() => composed.EvaluateAsync(2));

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity()
            .GetTagItem("motiv.proposition")
            .ShouldBe(composed.Description.Statement);
    }

    [Fact]
    public async Task Should_surface_a_concurrently_evaluated_operands_exception_unwrapped_with_exactly_one_span()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .BuildAsync((int _) => throw new InvalidOperationException("concurrent boom"))
            .Create("throws");
        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");
        var composed = isEven.AndConcurrently(throws);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => composed.EvaluateAsync(2));
        exception.Message.ShouldBe("concurrent boom");

        harness.Activities.Count.ShouldBe(1);
        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
    }

    [Fact]
    public async Task Should_surface_a_lifted_sync_operands_exception_unwrapped_within_an_async_composition()
    {
        using var harness = new TelemetryHarness();

        var throwsSync = Spec
            .Build((int _) => throw new InvalidOperationException("sync boom"))
            .Create("throws sync");
        var isEven = Spec.BuildAsync((int n) => Task.FromResult(n % 2 == 0)).Create("is even");
        var composed = isEven.And(throwsSync);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => composed.EvaluateAsync(2));
        exception.Message.ShouldBe("sync boom");

        harness.Activities.Count.ShouldBe(1);
        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
    }
}
