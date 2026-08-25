using System.Diagnostics;
using System.Diagnostics.Metrics;
using Motiv.Diagnostics;

namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// Pins the rules stack's published subscription names and instrument names.
/// </summary>
/// <remarks>
/// These names are the contract an operator's dashboard is built against, and a rename is invisible
/// at compile time and silent at runtime — a subscriber simply receives nothing. They are pinned as
/// literals here on purpose: asserting a constant against itself would pass through any rename.
/// </remarks>
[Collection(RulesTelemetryTestCollection.Name)]
public class RulesTelemetryNameTests
{
    [Fact]
    public void Should_publish_the_rules_stack_on_its_own_source_and_meter()
    {
        // The rules stack is on its own version train — core Motiv is v8 and frozen, this is 0.x and
        // still churning. Sharing a source would tie one's stability promise to the other's.
        MotivRulesTelemetry.SourceName.ShouldBe("Motiv.Serialization");
        MotivRulesTelemetry.MeterName.ShouldBe("Motiv.Serialization");
        MotivRulesTelemetry.SourceName.ShouldNotBe(MotivTelemetry.SourceName);
        MotivRulesTelemetry.MeterName.ShouldNotBe(MotivTelemetry.MeterName);
    }

    [Theory]
    [InlineData("motiv.rules.bind_failures")]
    [InlineData("motiv.rules.publish_conflicts")]
    [InlineData("motiv.rules.store.duration")]
    [InlineData("motiv.rules.catalog.size")]
    [InlineData("motiv.rules.generation")]
    [InlineData("motiv.rules.replica_lag")]
    [InlineData("motiv.rules.refreshes")]
    [InlineData("motiv.rules.rebuild.duration")]
    [InlineData("motiv.rules.decisions.dropped")]
    [InlineData("motiv.rules.decision_queue.depth")]
    [InlineData("motiv.rules.decision_batches.failed")]
    [InlineData("motiv.rules.break_glass.active")]
    [InlineData("motiv.rules.publishes_under_break_glass")]
    public void Should_publish_the_instrument(string name)
    {
        using var harness = new RulesTelemetryHarness();

        // Touching the class is what runs the static initializer that creates the meter and its
        // instruments; the InlineData strings are compile-time literals and never would.
        MotivRulesTelemetry.EnsureInitialized();

        harness.PublishedInstruments.ShouldContain(name);
    }

    [Fact]
    public void Should_deliver_measurements_to_a_listener_subscribed_via_the_published_meter_name()
    {
        var instruments = new List<string>();
        var enabled = new List<Instrument>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name != MotivRulesTelemetry.MeterName) return;

                instruments.Add(instrument.Name);
                enabled.Add(instrument);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.Start();

        try
        {
            MotivRulesTelemetry.EnsureInitialized();
            instruments.ShouldContain("motiv.rules.refreshes");
        }
        finally
        {
            foreach (var instrument in enabled)
                listener.DisableMeasurementEvents(instrument);
            listener.Dispose();
        }
    }

    [Fact]
    public void Should_deliver_spans_to_a_listener_subscribed_via_the_published_source_name()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MotivRulesTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };

        ActivitySource.AddActivityListener(listener);

        using (MotivRulesTelemetry.StartRuleEvaluation("a-rule", version: 3)) { }

        activities.ShouldHaveSingleItem();
        activities[0].OperationName.ShouldBe("motiv.rules.evaluate");
        activities[0].GetTagItem("motiv.rules.name").ShouldBe("a-rule");
        activities[0].GetTagItem("motiv.rules.version").ShouldBe(3);
    }
}
