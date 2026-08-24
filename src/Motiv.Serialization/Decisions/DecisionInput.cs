namespace Motiv.Serialization;

/// <summary>How much of the evaluated model a decision record was allowed to keep.</summary>
/// <remarks>
/// The kind is stored alongside the value because it is what a reader needs to know what the record is
/// worth for replay, and because it is a standing statement of the privacy posture that produced it.
/// </remarks>
public enum DecisionInputKind
{
    /// <summary>The whole model, as evaluated. Complete replay, raw PII — development only.</summary>
    Whole,

    /// <summary>An adopter-supplied projection of the model. Replay goes as far as the mask left it.</summary>
    Redacted,

    /// <summary>
    /// A key into the adopter's own system of record. The GDPR-clean posture: erase the subject there
    /// and the decision record survives without personal data, while replay correctly becomes
    /// impossible.
    /// </summary>
    Reference
}

/// <summary>
/// What a decision record kept of the model it was evaluated against, and under which posture.
/// </summary>
public sealed record DecisionInput
{
    private DecisionInput(DecisionInputKind kind, object? value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>The capture posture that produced <see cref="Value"/>.</summary>
    public DecisionInputKind Kind { get; }

    /// <summary>The captured value — the model, a projection of it, or a key naming it elsewhere.</summary>
    public object? Value { get; }

    /// <summary>Captures the model as evaluated. Development only — this stores whatever it holds.</summary>
    /// <param name="model">The evaluated model.</param>
    public static DecisionInput Whole(object? model) => new(DecisionInputKind.Whole, model);

    /// <summary>Captures an adopter-supplied projection of the model.</summary>
    /// <param name="projection">The masked view to store.</param>
    public static DecisionInput Redacted(object? projection) => new(DecisionInputKind.Redacted, projection);

    /// <summary>Captures only a key naming the model in the adopter's own system of record.</summary>
    /// <param name="key">The key.</param>
    public static DecisionInput Reference(string key) => new(DecisionInputKind.Reference, key);
}
