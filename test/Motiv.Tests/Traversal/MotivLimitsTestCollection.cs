namespace Motiv.Tests.Traversal;

/// <summary>
/// Serialises every test class that changes <see cref="MotivLimits" />. The limits are process-wide by
/// design — Motiv has no options object to scope them to — so xUnit's default cross-class parallelization
/// would let one class's lowered ceiling abort another class's perfectly ordinary composition.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class MotivLimitsTestCollection
{
    internal const string Name = "MotivLimits";
}
