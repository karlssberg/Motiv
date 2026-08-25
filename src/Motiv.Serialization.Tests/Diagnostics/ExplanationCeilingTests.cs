using Motiv.Diagnostics;

namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// The one invariant that makes a PII posture worth stating: it is stated once, and both the durable
/// decision log and the ephemeral traces obey it.
/// </summary>
/// <remarks>
/// <see cref="MotivTelemetry.ExplanationDetail"/> is process-wide mutable state, so these run
/// serialized with the rest of the rules-telemetry classes and each restores what it found.
/// </remarks>
[Collection(RulesTelemetryTestCollection.Name)]
public class ExplanationCeilingTests
{
    private sealed class Customer(string id)
    {
        public string Id { get; } = id;
    }

    private sealed class NullSink : IDecisionSink
    {
        public Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static async Task WithDetail(ExplanationDetail starting, Func<Task> body)
    {
        var previous = MotivTelemetry.ExplanationDetail;
        MotivTelemetry.ExplanationDetail = starting;
        try
        {
            await body();
        }
        finally
        {
            MotivTelemetry.ExplanationDetail = previous;
        }
    }

    [Fact]
    public void Should_have_no_opinion_when_no_capture_posture_has_been_registered()
    {
        // A registry with nothing in it has made no statement about PII, so it must not make one.
        new DecisionCaptureRegistry().ExplanationCeiling.ShouldBe(ExplanationDetail.Full);
    }

    [Fact]
    public void Should_leave_explanation_text_alone_when_the_adopter_stores_whole_models()
    {
        // StoreWhole already accepts raw model data in durable storage; trace text is strictly less
        // exposure than that, so there is nothing here to tighten.
        new DecisionCaptureRegistry().StoreWhole<Customer>()
            .ExplanationCeiling.ShouldBe(ExplanationDetail.Full);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_suppress_explanation_text_when_the_model_may_not_be_stored_raw(bool referenceOnly)
    {
        // Arrange
        var registry = new DecisionCaptureRegistry();

        // Act
        _ = referenceOnly
            ? registry.ReferenceOnly<Customer>(customer => customer.Id)
            : registry.Redact<Customer>(customer => new { customer.Id });

        // Assert — neither the projection nor the key selector touches assertion text, so an
        // adopter who said "not raw" cannot have meant "except in traces".
        registry.ExplanationCeiling.ShouldBe(ExplanationDetail.None);
    }

    [Fact]
    public void Should_take_the_strictest_posture_when_model_types_differ()
    {
        // A process-wide setting has to satisfy the strictest registration in the process.
        new DecisionCaptureRegistry()
            .StoreWhole<string>()
            .ReferenceOnly<Customer>(customer => customer.Id)
            .ExplanationCeiling.ShouldBe(ExplanationDetail.None);
    }

    [Fact]
    public async Task Should_tighten_the_span_tags_when_a_decision_log_is_created()
    {
        await WithDetail(ExplanationDetail.Full, async () =>
        {
            // Arrange
            var options = new DecisionLogOptions();
            options.Capture.ReferenceOnly<Customer>(customer => customer.Id);

            // Act
            await using var log = new DecisionLog(new NullSink(), options);

            // Assert — stated once, on the capture registry, and the traces follow.
            MotivTelemetry.ExplanationDetail.ShouldBe(ExplanationDetail.None);
        });
    }

    [Fact]
    public async Task Should_never_loosen_a_setting_the_adopter_has_already_tightened()
    {
        await WithDetail(ExplanationDetail.None, async () =>
        {
            // Arrange — StoreWhole's ceiling is Full, which is looser than what is already set.
            var options = new DecisionLogOptions();
            options.Capture.StoreWhole<Customer>();

            // Act
            await using var log = new DecisionLog(new NullSink(), options);

            // Assert — a ceiling, not an assignment. Otherwise the result would depend on whether the
            // host happened to configure telemetry before or after the decision log.
            MotivTelemetry.ExplanationDetail.ShouldBe(ExplanationDetail.None);
        });
    }

    /// <summary>
    /// The invariant end to end, rather than as two properties that happen to agree: a host that
    /// registers the GDPR-clean capture posture gets evaluation spans with no explanation text on
    /// them, without having configured telemetry at all.
    /// </summary>
    [Fact]
    public async Task Should_leave_no_explanation_text_on_a_span_once_the_posture_forbids_raw_models()
    {
        await WithDetail(ExplanationDetail.Full, async () =>
        {
            // Arrange
            var options = new DecisionLogOptions();
            options.Capture.ReferenceOnly<Customer>(customer => customer.Id);
            await using var log = new DecisionLog(new NullSink(), options);

            using var harness = new RulesTelemetryHarness(listenToCore: true);

            // An unnamed explanation proposition, so the WhenFalse text IS the assertion — and it
            // templates the model. This is the case the whole control exists for: nothing about the
            // spec says "private", only the capture posture does.
            var spec = Spec.Build((Customer c) => c.Id.Length > 8)
                .WhenTrue("customer is known")
                .WhenFalse(c => $"customer {c.Id} is unknown")
                .Create();

            // Act
            var result = spec.Evaluate(new Customer("cust-42"));

            // The text that would have been exported, had the posture not forbidden it.
            result.Assertions.ShouldBe(["customer cust-42 is unknown"]);

            // Assert
            var span = harness.SingleActivity("motiv.evaluate");
            span.GetTagItem("motiv.satisfied").ShouldBe(false);
            span.GetTagItem("motiv.reason").ShouldBeNull();
            span.GetTagItem("motiv.assertions").ShouldBeNull();
        });
    }

    [Fact]
    public async Task Should_leave_a_host_with_no_audited_rules_exactly_as_it_was()
    {
        await WithDetail(ExplanationDetail.ReasonOnly, async () =>
        {
            // Arrange — a decision log with no posture registered has nothing to say about PII.
            await using var log = new DecisionLog(new NullSink(), new DecisionLogOptions());

            // Assert
            MotivTelemetry.ExplanationDetail.ShouldBe(ExplanationDetail.ReasonOnly);
            await Task.CompletedTask;
        });
    }
}
