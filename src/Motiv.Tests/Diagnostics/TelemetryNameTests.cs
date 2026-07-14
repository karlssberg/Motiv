using System.Diagnostics;
using System.Diagnostics.Metrics;
using Motiv.Diagnostics;

namespace Motiv.Tests.Diagnostics;

/// <summary>
/// Pins the publicly published subscription names to the source and meter Motiv actually registers. A consumer
/// subscribes with these constants, so a drift between them and the real names would silently emit nothing.
/// </summary>
[Collection(TelemetryTestCollection.Name)]
public class TelemetryNameTests
{
    [Fact]
    public void Should_deliver_spans_to_a_listener_subscribed_via_the_published_source_name()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MotivTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };

        ActivitySource.AddActivityListener(listener);

        Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(2);

        activities.ShouldHaveSingleItem();
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
                if (instrument.Meter.Name != MotivTelemetry.MeterName) return;

                instruments.Add(instrument.Name);
                enabled.Add(instrument);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.Start();

        try
        {
            // The evaluation is what forces Motiv's instruments into existence: the constants above are
            // compile-time literals, so naming them never runs the static initializer that creates the meter.
            Spec.Build((int n) => n % 2 == 0).Create("is even").Evaluate(2);

            instruments.ShouldContain("motiv.evaluations");
            instruments.ShouldContain("motiv.evaluation.duration");
        }
        finally
        {
            // On net8.0, Instrument.Enabled does not flip back to false when a still-subscribed MeterListener is
            // disposed (fixed in net9.0+), which would leave the instruments enabled for every later test.
            foreach (var instrument in enabled)
                listener.DisableMeasurementEvents(instrument);

            listener.Dispose();
        }
    }
}
