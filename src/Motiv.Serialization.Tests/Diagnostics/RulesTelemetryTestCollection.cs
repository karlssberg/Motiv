namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// Serializes every test class that touches process-wide telemetry state — which means every class
/// that uses <see cref="RulesTelemetryHarness"/>, <strong>and every class that constructs a
/// <see cref="DecisionLog"/></strong>.
/// </summary>
/// <remarks>
/// <para>
/// Two distinct pieces of global state, one collection.
/// </para>
/// <para>
/// The harness registers a process-wide <see cref="System.Diagnostics.ActivityListener"/> and
/// <see cref="System.Diagnostics.Metrics.MeterListener"/> keyed on the rules stack's source and meter
/// name, so xUnit's default cross-class parallelization would let two telemetry test classes capture
/// each other's spans and measurements. Mirrors <c>Motiv.Tests</c>' <c>TelemetryTestCollection</c>,
/// which exists for the same reason over core's source.
/// </para>
/// <para>
/// <strong>Constructing a <see cref="DecisionLog"/> is the second reason, and the less obvious one.</strong>
/// A log whose capture registry names a <c>Redact</c> or <c>ReferenceOnly</c> posture tightens
/// <c>MotivTelemetry.ExplanationDetail</c> to <c>None</c> for the rest of the process — deliberately,
/// and deliberately without ever restoring it, so that a host cannot end up exporting assertion text
/// it configured the log not to store. Correct for a host, which builds one log at startup. Inside a
/// test run it means any class constructing such a log while another asserts on explanation text
/// fails that other class, at whatever moment the two happen to overlap. So the rule is blunt on
/// purpose: <em>if a class news up a <see cref="DecisionLog"/>, it belongs here</em> — easier to keep
/// true than a rule about which postures the log happened to be given.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class RulesTelemetryTestCollection
{
    internal const string Name = "RulesTelemetry";
}
