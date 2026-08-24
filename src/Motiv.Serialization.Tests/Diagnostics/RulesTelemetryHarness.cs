using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// Subscribes to the rules stack's own source and meter — <see cref="MotivRulesTelemetry.SourceName"/>
/// and <see cref="MotivRulesTelemetry.MeterName"/> — and nothing else, so a measurement captured here
/// is one the rules stack emitted rather than one core <c>Motiv</c> did.
/// </summary>
/// <remarks>
/// Most of the <c>motiv.rules.*</c> instruments are observable: they are read from a live subject
/// (a decision log, a scope, a break-glass registration) rather than pushed at a call site, so
/// nothing arrives until <see cref="Collect"/> asks for it. Call it before asserting.
/// </remarks>
internal sealed class RulesTelemetryHarness : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly List<Activity> _activities = [];
    private readonly List<Measurement> _measurements = [];
    private readonly List<Instrument> _enabled = [];

    public RulesTelemetryHarness(bool listenToCore = false)
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == MotivRulesTelemetry.SourceName
                || (listenToCore && source.Name == Motiv.Diagnostics.MotivTelemetry.SourceName),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (_activities) _activities.Add(activity); }
        };

        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != MotivRulesTelemetry.MeterName) return;

                listener.EnableMeasurementEvents(instrument);
                lock (_enabled) _enabled.Add(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
        _meterListener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) => Capture(instrument, measurement, tags));

        _meterListener.Start();
    }

    public IReadOnlyList<Activity> Activities { get { lock (_activities) return [.. _activities]; } }

    public IReadOnlyList<Measurement> Measurements { get { lock (_measurements) return [.. _measurements]; } }

    /// <summary>The instrument names this listener was offered — what the meter actually publishes.</summary>
    public IReadOnlyList<string> PublishedInstruments
    {
        get { lock (_enabled) return [.. _enabled.Select(instrument => instrument.Name)]; }
    }

    /// <summary>Polls every observable instrument, so the readings they expose become measurements.</summary>
    public void Collect() => _meterListener.RecordObservableInstruments();

    public IReadOnlyList<Measurement> For(string instrument) =>
        [.. Measurements.Where(measurement => measurement.Instrument == instrument)];

    public Measurement Single(string instrument) => For(instrument).Single();

    public Activity SingleActivity(string name) =>
        Activities.Single(activity => activity.OperationName == name);

    private void Capture(
        Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copied = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
            copied[tag.Key] = tag.Value;

        lock (_measurements) _measurements.Add(new Measurement(instrument.Name, value, copied));
    }

    public void Dispose()
    {
        _activityListener.Dispose();

        // Mirrors Motiv.Tests' TelemetryHarness: .NET 8's Meter does not reliably flip
        // Instrument.Enabled back to false when a still-subscribed listener is disposed, so this
        // disables each one explicitly and the flag reads false again on every target framework.
        lock (_enabled)
            foreach (var instrument in _enabled)
                _meterListener.DisableMeasurementEvents(instrument);

        _meterListener.Dispose();
    }

    internal sealed record Measurement(
        string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)
    {
        public object? Tag(string key) => Tags.TryGetValue(key, out var value) ? value : null;
    }
}
