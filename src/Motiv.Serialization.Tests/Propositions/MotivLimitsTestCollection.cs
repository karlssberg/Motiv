namespace Motiv.Serialization.Tests.Propositions;

/// <summary>
/// Serialises the classes in this assembly that change <see cref="MotivLimits" />. The limits are
/// process-wide by design — Motiv has no options object to scope them to — so a lowered ceiling would
/// otherwise abort a perfectly ordinary composition in whatever else xunit was running at the time.
/// The twin of <c>Motiv.Tests</c>' collection of the same name; collections do not span assemblies.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class MotivLimitsTestCollection
{
    internal const string Name = "MotivLimits";
}
