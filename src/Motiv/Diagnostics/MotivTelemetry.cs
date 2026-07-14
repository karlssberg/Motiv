using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Motiv.Diagnostics;

/// <summary>
/// Owns Motiv's OpenTelemetry primitives. Nothing is emitted unless a listener subscribes to the
/// <c>Motiv</c> activity source or meter, so instrumentation is inert by default.
/// </summary>
/// <remarks>
/// Subscribe with <see cref="SourceName" /> and <see cref="MeterName" /> rather than string literals — a
/// mistyped name is not an error, it is silence.
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(tracing => tracing.AddSource(MotivTelemetry.SourceName))
///     .WithMetrics(metrics => metrics.AddMeter(MotivTelemetry.MeterName));
/// </code>
/// </remarks>
public static class MotivTelemetry
{
    /// <summary>
    /// The name of Motiv's activity source. Pass this to <c>AddSource</c> to receive Motiv's evaluation spans.
    /// </summary>
    public const string SourceName = "Motiv";

    /// <summary>
    /// The name of Motiv's meter. Pass this to <c>AddMeter</c> to receive Motiv's evaluation metrics.
    /// </summary>
    public const string MeterName = "Motiv";

    /// <summary>The name given to every evaluation activity.</summary>
    internal const string ActivityName = "motiv.evaluate";

    internal static readonly ActivitySource ActivitySource =
        new(SourceName, typeof(MotivTelemetry).Assembly.GetName().Version?.ToString());

    private static readonly Meter Meter =
        new(MeterName, typeof(MotivTelemetry).Assembly.GetName().Version?.ToString());

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
