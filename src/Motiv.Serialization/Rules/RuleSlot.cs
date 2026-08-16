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
    /// <remarks>
    /// A <em>publish</em>, not any state change. Use <see cref="WithBinding"/> for a cascaded rebind,
    /// which changes the binding without answering the question the quarantine asks — see there.
    /// </remarks>
    public RuleSlot WithState(object state) => new(state, []);

    /// <summary>
    /// The slot after a cascaded rebind: a new binding, and the quarantine carried across untouched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A rebind re-binds whatever document the rule <em>currently carries</em>. For a quarantined rule
    /// that document is the compiled default — the stored one is precisely the document that would not
    /// bind — so the rebind succeeding says nothing whatever about whether the stored document would
    /// now bind. The quarantine is still true, and <see cref="WithState"/> would clear it, reporting a
    /// broken rule as healthy while it quietly kept running its default. A rebind is not a repair.
    /// </para>
    /// <para>
    /// <strong>Authored propositions deliberately do the opposite</strong> —
    /// <c>AuthoredProposition.WithBinding</c> clears quarantine on rebind — and that is correct there,
    /// not an inconsistency. A quarantined authored proposition resolves to nothing: no overlay entry,
    /// no graph edges, no participant enrolment, so a cascade can never reach one, and anything that
    /// does reach it has re-bound the very document that was quarantined. A quarantined <em>rule</em>
    /// stays enrolled, because a rule is registered by <see cref="RuleSet.Add"/> independently of
    /// whether its stored document later bound, and it keeps the edges of the default it is running.
    /// That is why the two directions differ.
    /// </para>
    /// </remarks>
    public RuleSlot WithBinding(object state) => new(state, Quarantine);

    /// <summary>The slot after a stored document failed to bind: the binding is kept, the reason recorded.</summary>
    public RuleSlot WithQuarantine(IReadOnlyList<RuleError> quarantine) => new(State, quarantine);
}
