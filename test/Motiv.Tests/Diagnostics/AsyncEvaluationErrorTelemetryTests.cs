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

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => throws.EvaluateAsync(1).AsTask());
        exception.Message.ShouldBe("async boom");
        exception.ShouldBeSameAs(original);

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

        await Should.ThrowAsync<InvalidOperationException>(() => throws.EvaluateAsync(1).AsTask());

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
        var isEven = Spec.BuildAsync((int n) => new ValueTask<bool>(n % 2 == 0)).Create("is even");
        var composed = isEven.And(throws);

        await Should.ThrowAsync<InvalidOperationException>(() => composed.EvaluateAsync(2).AsTask());

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
        var isEven = Spec.BuildAsync((int n) => new ValueTask<bool>(n % 2 == 0)).Create("is even");
        var composed = isEven.AndConcurrently(throws);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => composed.EvaluateAsync(2).AsTask());
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
        var isEven = Spec.BuildAsync((int n) => new ValueTask<bool>(n % 2 == 0)).Create("is even");
        var composed = isEven.And(throwsSync);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => composed.EvaluateAsync(2).AsTask());
        exception.Message.ShouldBe("sync boom");

        harness.Activities.Count.ShouldBe(1);
        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
    }

    [Fact]
    public async Task Should_record_a_callers_own_cancellation_as_not_an_error()
    {
        using var harness = new TelemetryHarness();
        using var cancellation = new CancellationTokenSource();

        Func<int, CancellationToken, ValueTask<bool>> slowPredicate = (_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return new ValueTask<bool>(true);
        };
        var slow = Spec.BuildAsync(slowPredicate).Create("slow");

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await slow.EvaluateAsync(1, cancellation.Token));

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
        activity.GetTagItem("error.type").ShouldBeNull();
        activity.Events.ShouldNotContain(activityEvent => activityEvent.Name == "exception");

        harness.Measurements.Count(measurement => measurement.Instrument == "motiv.evaluations").ShouldBe(1);
        var count = harness.SingleMeasurement("motiv.evaluations");
        count.Tags["motiv.cancelled"].ShouldBe(true);
        count.Tags.ContainsKey("error.type").ShouldBeFalse();
    }

    [Fact]
    public async Task Should_record_cancellation_as_an_error_when_the_callers_token_was_never_signalled()
    {
        using var harness = new TelemetryHarness();

        // The caller's own token (default, never cancelled) is not signalled, so this OperationCanceledException
        // cannot be attributed to caller intent — it must be reported as a genuine failure, exactly like any
        // other exception.
        var throws = Spec
            .BuildAsync((int _) => throw new OperationCanceledException("boom"))
            .Create("throws");

        await Should.ThrowAsync<OperationCanceledException>(() => throws.EvaluateAsync(1).AsTask());

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.OperationCanceledException");
        activity.GetTagItem("motiv.cancelled").ShouldBeNull();

        var count = harness.SingleMeasurement("motiv.evaluations");
        count.Tags["error.type"].ShouldBe("System.OperationCanceledException");
        count.Tags.ContainsKey("motiv.cancelled").ShouldBeFalse();
    }

    [Fact]
    public async Task Should_attribute_a_composed_async_propositions_cancellation_to_the_root_with_a_cancelled_not_errored_shape()
    {
        using var harness = new TelemetryHarness();
        using var cancellation = new CancellationTokenSource();

        Func<int, CancellationToken, ValueTask<bool>> slowPredicate = (_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return new ValueTask<bool>(true);
        };
        var slow = Spec.BuildAsync(slowPredicate).Create("slow");
        var isEven = Spec.BuildAsync((int n) => new ValueTask<bool>(n % 2 == 0)).Create("is even");
        var composed = isEven.And(slow);

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await composed.EvaluateAsync(2, cancellation.Token));

        harness.Activities.Count.ShouldBe(1);
        var activity = harness.SingleActivity();
        activity.GetTagItem("motiv.proposition").ShouldBe(composed.Description.Statement);
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
        activity.GetTagItem("error.type").ShouldBeNull();

        harness.SingleMeasurement("motiv.evaluations").Tags["motiv.cancelled"].ShouldBe(true);
    }
}
