namespace Motiv.Serialization;

/// <summary>
/// One rule's place in a generation: the bound state it evaluates through, and why its stored
/// document could not be applied. Both live here rather than on the rule so that a whole world moves
/// with a single reference write — a rule that held either itself would be a second write, and two
/// writes are a straddle.
/// </summary>
/// <remarks>
/// <c>State</c> is typed <see cref="object"/> because the state type closes over the rule's own
/// generic arguments; the rule casts it back on the way out, which is a castclass on a path that
/// already dereferences two fields.
/// </remarks>
internal sealed class RuleSlot(object state, IReadOnlyList<RuleError> quarantine)
{
    public object State { get; } = state;

    public IReadOnlyList<RuleError> Quarantine { get; } = quarantine;

    /// <summary>
    /// The slot after a successful publish. Quarantine is dropped rather than carried: a quarantine
    /// says "running a compiled default in place of a stored document that would not bind", and a
    /// successful publish is exactly what stops that being true.
    /// </summary>
    public RuleSlot WithState(object state) => new(state, []);

    /// <summary>The slot after a stored document failed to bind: the binding is kept, the reason recorded.</summary>
    public RuleSlot WithQuarantine(IReadOnlyList<RuleError> quarantine) => new(State, quarantine);
}
