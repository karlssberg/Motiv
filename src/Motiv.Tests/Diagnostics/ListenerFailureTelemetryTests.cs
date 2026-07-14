namespace Motiv.Tests.Diagnostics;

/// <summary>
/// Covers the case where telemetry infrastructure itself misbehaves — a user's <c>ActivityListener</c> callback
/// (or an exporter it drives synchronously via <c>Activity.Dispose</c>) throws. That must never be attributed to
/// the evaluation: the evaluation succeeded, so it must return normally and be counted exactly once, not be
/// reported as a second, errored evaluation.
/// </summary>
[Collection(TelemetryTestCollection.Name)]
public class ListenerFailureTelemetryTests
{
    [Fact]
    public void Should_return_the_result_normally_when_the_ActivityStopped_listener_throws()
    {
        using var harness = new ThrowingListenerTelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        var result = isEven.Evaluate(2);

        result.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_record_the_evaluation_exactly_once_when_the_ActivityStopped_listener_throws()
    {
        using var harness = new ThrowingListenerTelemetryHarness();

        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");

        isEven.Evaluate(2);

        harness.Measurements.Count(measurement => measurement.Instrument == "motiv.evaluations").ShouldBe(1);
        harness.SingleMeasurement("motiv.evaluations").Tags.ContainsKey("error.type").ShouldBeFalse();
    }

    [Fact]
    public void Should_preserve_the_evaluations_own_exception_when_the_ActivityStopped_listener_throws()
    {
        using var harness = new ThrowingListenerTelemetryHarness();
        var original = new ArgumentException("evaluation boom");

        var throws = Spec.Build((int _) => throw original).Create("throws");

        var exception = Should.Throw<ArgumentException>(() => throws.Evaluate(1));
        exception.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task Should_preserve_the_async_evaluations_own_exception_when_the_ActivityStopped_listener_throws()
    {
        using var harness = new ThrowingListenerTelemetryHarness();
        var original = new ArgumentException("async evaluation boom");

        var throws = Spec.BuildAsync((int _) => throw original).Create("throws");

        var exception = await Should.ThrowAsync<ArgumentException>(() => throws.EvaluateAsync(1));
        exception.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task Should_preserve_the_cancellation_exception_when_the_ActivityStopped_listener_throws()
    {
        using var harness = new ThrowingListenerTelemetryHarness();
        using var cancellation = new CancellationTokenSource();

        Func<int, CancellationToken, Task<bool>> slowPredicate = (_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        };
        var slow = Spec.BuildAsync(slowPredicate).Create("slow");

        // A caller catching OperationCanceledException for graceful shutdown must still receive it — not the
        // listener's own exception thrown from Activity.Dispose inside the boundary's catch block.
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await slow.EvaluateAsync(1, cancellation.Token));
    }
}
