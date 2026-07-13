using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Diagnostics;

/// <summary>
/// Owns Motiv's OpenTelemetry primitives. Nothing is emitted unless a listener subscribes to the
/// <c>Motiv</c> activity source or meter, so instrumentation is inert by default.
/// </summary>
internal static class MotivTelemetry
{
    /// <summary>The name of both the activity source and the meter. Users subscribe with this name.</summary>
    internal const string SourceName = "Motiv";

    /// <summary>The name given to every evaluation activity.</summary>
    internal const string ActivityName = "motiv.evaluate";

    internal static readonly ActivitySource ActivitySource =
        new(SourceName, typeof(MotivTelemetry).Assembly.GetName().Version?.ToString());

    private static readonly Meter Meter =
        new(SourceName, typeof(MotivTelemetry).Assembly.GetName().Version?.ToString());

    internal static readonly Counter<long> Evaluations =
        Meter.CreateCounter<long>(
            "motiv.evaluations",
            "{evaluation}",
            "The number of proposition evaluations.");

    internal static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>(
            "motiv.evaluation.duration",
            "s",
            "The duration of proposition evaluations.");

    /// <summary>
    /// Gets a value indicating whether anything is listening. When <c>false</c>, evaluation must take the
    /// uninstrumented path — no activity, no timestamp, no result inspection.
    /// </summary>
    internal static bool IsEnabled =>
        ActivitySource.HasListeners() || Evaluations.Enabled || Duration.Enabled;
}
