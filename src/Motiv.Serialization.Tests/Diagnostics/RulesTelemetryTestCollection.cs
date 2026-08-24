namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// Serializes every test class that uses <see cref="RulesTelemetryHarness"/>.
/// </summary>
/// <remarks>
/// The harness registers a process-wide <see cref="System.Diagnostics.ActivityListener"/> and
/// <see cref="System.Diagnostics.Metrics.MeterListener"/> keyed on the rules stack's source and meter
/// name, so xUnit's default cross-class parallelization would let two telemetry test classes capture
/// each other's spans and measurements. Mirrors <c>Motiv.Tests</c>' <c>TelemetryTestCollection</c>,
/// which exists for the same reason over core's source.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class RulesTelemetryTestCollection
{
    internal const string Name = "RulesTelemetry";
}
