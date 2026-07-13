using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Tests.Diagnostics;

internal sealed class TelemetryHarness : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly List<Activity> _activities = [];
    private readonly List<Measurement> _measurements = [];
    private readonly List<Instrument> _enabledInstruments = [];

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
                if (instrument.Meter.Name != "Motiv") return;

                listener.EnableMeasurementEvents(instrument);
                _enabledInstruments.Add(instrument);
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

        // .NET 8's Meter implementation doesn't reliably flip Instrument.Enabled back to false when a
        // MeterListener is disposed while still subscribed (fixed in .NET 9+). Explicitly disabling each
        // instrument this listener enabled works around that, so IsEnabled correctly reports false again
        // once this harness is gone, on every target framework.
        foreach (var instrument in _enabledInstruments)
            _meterListener.DisableMeasurementEvents(instrument);

        _meterListener.Dispose();
    }

    internal sealed record Measurement(
        string Instrument,
        double Value,
        IReadOnlyDictionary<string, object?> Tags)
    {
        public string Instrument { get; } = Instrument;
        public double Value { get; } = Value;
        public IReadOnlyDictionary<string, object?> Tags { get; } = Tags;
    }
}
