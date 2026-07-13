using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Tests.Diagnostics;

internal sealed class TelemetryHarness : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly List<Activity> _activities = [];
    private readonly List<Measurement> _measurements = [];

    public TelemetryHarness()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Motiv",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(activity)
        };

        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Motiv")
                    listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
        _meterListener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) => Capture(instrument, measurement, tags));

        _meterListener.Start();
    }

    public IReadOnlyList<Activity> Activities => _activities;

    public IReadOnlyList<Measurement> Measurements => _measurements;

    public Activity SingleActivity() => _activities.Single();

    public Measurement SingleMeasurement(string instrument) =>
        _measurements.Single(measurement => measurement.Instrument == instrument);

    private void Capture(
        Instrument instrument,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copied = new Dictionary<string, object?>();
        foreach (var tag in tags)
            copied[tag.Key] = tag.Value;

        _measurements.Add(new Measurement(instrument.Name, value, copied));
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    internal sealed record Measurement(
        string Instrument,
        double Value,
        IReadOnlyDictionary<string, object?> Tags);
}
