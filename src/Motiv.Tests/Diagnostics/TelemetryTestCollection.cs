namespace Motiv.Tests.Diagnostics;

/// <summary>
/// Serializes every test class that uses <see cref="TelemetryHarness" />. The harness registers a
/// process-wide <see cref="System.Diagnostics.ActivityListener" /> and <see cref="System.Diagnostics.Metrics.MeterListener" />
/// keyed on the "Motiv" source/meter name, so xUnit's default cross-class parallelization would let
/// concurrently-running telemetry test classes cross-capture each other's activities and measurements.
/// Any test class that uses <see cref="TelemetryHarness" /> should apply <c>[Collection(TelemetryTestCollection.Name)]</c>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class TelemetryTestCollection
{
    internal const string Name = "Telemetry";
}
