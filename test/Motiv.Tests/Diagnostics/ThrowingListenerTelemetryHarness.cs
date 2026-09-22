using System.Diagnostics;
using System.Diagnostics.Metrics;
using Motiv.Diagnostics;

namespace Motiv.Tests.Diagnostics;

/// <summary>
/// A telemetry harness whose <c>ActivityStopped</c> callback throws, standing in for a misbehaving
/// <c>ActivityListener</c> (or exporter enqueue) registered by a caller. Used to verify that a listener's own
/// failure cannot corrupt an evaluation that otherwise completed successfully — see
/// <see cref="ListenerFailureTelemetryTests" />.
/// </summary>
internal sealed class ThrowingListenerTelemetryHarness : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly List<TelemetryHarness.Measurement> _measurements = [];
    private readonly List<Instrument> _enabledInstruments = [];

    public ThrowingListenerTelemetryHarness()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MotivTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _ => throw new InvalidOperationException("listener boom")
        };

        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != MotivTelemetry.SourceName) return;

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

    public IReadOnlyList<TelemetryHarness.Measurement> Measurements => _measurements;

    public TelemetryHarness.Measurement SingleMeasurement(string instrument) =>
        _measurements.Single(measurement => measurement.Instrument == instrument);

    private void Capture(
        Instrument instrument,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copied = new Dictionary<string, object?>();
        foreach (var tag in tags)
            copied[tag.Key] = tag.Value;

        _measurements.Add(new TelemetryHarness.Measurement(instrument.Name, value, copied));
    }

    public void Dispose()
    {
        _activityListener.Dispose();

        // See TelemetryHarness.Dispose for why this loop is necessary on .NET 8.
        foreach (var instrument in _enabledInstruments)
            _meterListener.DisableMeasurementEvents(instrument);

        _meterListener.Dispose();
    }
}
