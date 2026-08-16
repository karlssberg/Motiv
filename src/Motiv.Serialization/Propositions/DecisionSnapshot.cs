namespace Motiv.Serialization;

/// <summary>
/// One decision's world, held still. Every rule evaluated while this is open resolves against the
/// generation it pinned, so a decision spanning several rules sees a set that really was published
/// together.
/// </summary>
/// <remarks>
/// <para>
/// A single shared generation makes a publish one write instead of many — but a caller evaluating two
/// rules still performs two reads, and a swap can land between them. The result, one rule from the
/// new world and one from the old, is a combination that never existed anywhere: not staleness, which
/// is explicable ("you got yesterday's policy"), but incoherence, which is not. This closes that gap.
/// </para>
/// <para>
/// The pin follows the async flow, so it survives <c>await</c>. Nesting is safe: an inner pin reuses
/// the outer one and disposing it does not end the decision.
/// </para>
/// </remarks>
public sealed class DecisionSnapshot : IDisposable
{
    private readonly IDisposable _pin;

    internal DecisionSnapshot(BindingScope scope)
    {
        _pin = scope.Pin();
        Generation = scope.Active.Sequence;
    }

    /// <summary>Where both stores stood in the pinned world — what a response stamps as its fencing token.</summary>
    public StoreGeneration Generation { get; }

    /// <summary>Releases the pin, unless an outer pin owns the decision.</summary>
    public void Dispose() => _pin.Dispose();
}
