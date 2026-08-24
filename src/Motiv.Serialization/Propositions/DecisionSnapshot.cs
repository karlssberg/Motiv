namespace Motiv.Serialization;

/// <summary>
/// One decision's world, held still. Every rule evaluated while this is open resolves against the
/// generation it pinned, so a decision spanning several rules sees a set that really was published
/// together — and every record those evaluations leave carries this decision's
/// <see cref="CorrelationId"/>, because they were one decision.
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
/// the outer one — including its correlation id and caller — and disposing it does not end the
/// decision.
/// </para>
/// </remarks>
public sealed class DecisionSnapshot : IDisposable
{
    private static readonly AsyncLocal<DecisionSnapshot?> Ambient = new();

    private readonly IDisposable _pin;
    private readonly DecisionSnapshot? _outer;
    private readonly bool _owned;

    internal DecisionSnapshot(BindingScope scope, string? correlationId = null, string? caller = null)
    {
        _pin = scope.Pin();
        Generation = scope.Active.Sequence;

        _outer = Ambient.Value;
        if (_outer is null)
        {
            // A decision always has an identity, whether or not anyone named it: a record from an
            // unpinned or unnamed evaluation still has to be findable.
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N");
            Caller = caller;
            _owned = true;
            Ambient.Value = this;
        }
        else
        {
            // An inner pin joins the decision already in progress rather than starting a second one
            // or relabelling it — the same nesting rule the generation pin follows.
            CorrelationId = _outer.CorrelationId;
            Caller = _outer.Caller;
        }
    }

    /// <summary>
    /// The decision currently in progress on this flow, or null when nothing is pinned. Read by the
    /// decision log so an evaluation need not be handed its own correlation id.
    /// </summary>
    public static DecisionSnapshot? Current => Ambient.Value;

    /// <summary>Where both stores stood in the pinned world — what a response stamps as its fencing token.</summary>
    public StoreGeneration Generation { get; }

    /// <summary>
    /// The identity every decision record from this decision carries. Supplied by the host — a trace
    /// id, a request id — or minted here when it supplies none.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>Who this decision was taken for, or null when nothing named them.</summary>
    public string? Caller { get; }

    /// <summary>Releases the pin, unless an outer pin owns the decision.</summary>
    public void Dispose()
    {
        if (_owned)
            Ambient.Value = _outer;

        _pin.Dispose();
    }
}
