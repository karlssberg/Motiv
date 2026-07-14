using System.Diagnostics.Metrics;
using Motiv.Diagnostics;

namespace Motiv.Tests.Diagnostics;

/// <summary>
/// A harness that attaches only a <see cref="MeterListener" /> — no <see cref="System.Diagnostics.ActivityListener" />
/// — so <c>ActivitySource.HasListeners()</c> stays <c>false</c> and <c>EvaluationScope.Start</c> never opens an
/// activity. Used to verify the metrics-only guarantee: with no tracing listener attached, an evaluation's
/// <c>Reason</c>/<c>Assertions</c> are never resolved, no matter what a metrics-only consumer is doing.
/// </summary>
internal sealed class MetricsOnlyTelemetryHarness : IDisposable
{
    private readonly MeterListener _meterListener;
    private readonly List<TelemetryHarness.Measurement> _measurements = [];
    private readonly List<Instrument> _enabledInstruments = [];

    public MetricsOnlyTelemetryHarness()
    {
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
        // See TelemetryHarness.Dispose for why this loop is necessary on .NET 8.
        foreach (var instrument in _enabledInstruments)
            _meterListener.DisableMeasurementEvents(instrument);

        _meterListener.Dispose();
    }
}
