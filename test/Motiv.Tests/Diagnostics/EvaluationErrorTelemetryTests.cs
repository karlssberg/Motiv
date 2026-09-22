using System.Diagnostics;

namespace Motiv.Tests.Diagnostics;

[Collection(TelemetryTestCollection.Name)]
public class EvaluationErrorTelemetryTests
{
    [Fact]
    public void Should_mark_the_span_as_errored_and_rethrow_the_original_exception_unwrapped()
    {
        using var harness = new TelemetryHarness();
        var original = new InvalidOperationException("boom");

        var throws = Spec.Build((int _) => throw original).Create("throws");

        var exception = Should.Throw<InvalidOperationException>(() => throws.Evaluate(1));
        exception.Message.ShouldBe("boom");
        exception.ShouldBeSameAs(original);

        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        activity.GetTagItem("motiv.satisfied").ShouldBeNull();
    }

    [Fact]
    public void Should_record_the_error_type_on_both_instruments()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .Build((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");

        Should.Throw<InvalidOperationException>(() => throws.Evaluate(1));

        harness.SingleMeasurement("motiv.evaluations")
            .Tags["error.type"].ShouldBe("System.InvalidOperationException");
        harness.SingleMeasurement("motiv.evaluation.duration")
            .Tags["error.type"].ShouldBe("System.InvalidOperationException");
    }

    [Fact]
    public void Should_attribute_the_error_to_the_root_proposition_not_the_failing_operand()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .Build((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");
        var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
        var composed = isEven & throws;

        Should.Throw<InvalidOperationException>(() => composed.Evaluate(2));

        harness.Activities.Count.ShouldBe(1);
        harness.SingleActivity()
            .GetTagItem("motiv.proposition")
            .ShouldBe(composed.Description.Statement);
    }

    [Fact]
    public void Should_add_an_exception_event_with_type_and_message_tags()
    {
        using var harness = new TelemetryHarness();

        var throws = Spec
            .Build((int _) => throw new InvalidOperationException("boom"))
            .Create("throws");

        Should.Throw<InvalidOperationException>(() => throws.Evaluate(1));

        var activity = harness.SingleActivity();
        var exceptionEvent = activity.Events.Single(activityEvent => activityEvent.Name == "exception");
        var tags = exceptionEvent.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);

        tags["exception.type"].ShouldBe("System.InvalidOperationException");
        tags["exception.message"].ShouldBe("boom");
        tags.ShouldContainKey("exception.stacktrace");
    }

    [Fact]
    public void Should_return_the_result_normally_when_resolving_its_explanation_text_throws()
    {
        using var harness = new TelemetryHarness();

        // WhenFalse is a delegate, so its string is resolved lazily when Reason/Assertions are read — exactly
        // what a tracing listener's span-tagging does. A succeeding evaluation must not become a throwing one
        // just because something started listening.
        var proposition = Spec
            .Build((int _) => false)
            .WhenTrue("value")
            .WhenFalse((int _) => throw new InvalidOperationException("explanation boom"))
            .Create();

        var result = proposition.Evaluate(1);

        result.Satisfied.ShouldBeFalse();
        var activity = harness.SingleActivity();
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
        activity.GetTagItem("motiv.satisfied").ShouldBe(false);
        activity.GetTagItem("motiv.reason").ShouldBeNull();
        activity.GetTagItem("motiv.assertions").ShouldBeNull();
    }
}
