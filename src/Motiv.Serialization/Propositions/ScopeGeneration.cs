namespace Motiv.Serialization;

/// <summary>
/// One coherent world: every authored proposition, every rule's binding, the graph relating them,
/// and where both stores stood when it was built. Immutable once constructed and replaced wholesale
/// — never edited — so a reader holding one holds a set that really was published together.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a straddle unrepresentable. Before it, a publish wrote the overlay, the graph,
/// the participant table and each rule's own state separately, so a reader between two of those
/// writes could observe a combination no publish ever produced. For a product whose promise is
/// explainability, an internally inconsistent justification is the one failure that cannot be
/// tolerated.
/// </para>
/// <para>
/// Holding a generation is not by itself enough: a caller evaluating two rules performs two reads
/// and can still see two worlds. <c>DecisionSnapshot</c> is the other half.
/// </para>
/// </remarks>
internal sealed class ScopeGeneration
{
    public ScopeGeneration(
        SpecRegistry registry,
        StoreGeneration sequence,
        PropositionOverlay overlay,
        DependencyGraph graph,
        IReadOnlyDictionary<NodeId, IRebindable> participants,
        IReadOnlyDictionary<string, PropositionSet.Authored> authored,
        RuleSlot?[] ruleSlots)
    {
        Sequence = sequence;
        Overlay = overlay;
        Graph = graph;
        Participants = participants;
        Authored = authored;
        RuleSlots = ruleSlots;
        Source = new LayeredSpecSource(overlay, registry);
    }

    /// <summary>Where both stores stood when this world was built.</summary>
    public StoreGeneration Sequence { get; }

    /// <summary>The authored layer as it resolves in this world.</summary>
    public PropositionOverlay Overlay { get; }

    /// <summary>Who references whom in this world.</summary>
    public DependencyGraph Graph { get; }

    /// <summary>Every node that must be rebound when a proposition it references is republished.</summary>
    public IReadOnlyDictionary<NodeId, IRebindable> Participants { get; }

    /// <summary>Every authored proposition, by name.</summary>
    public IReadOnlyDictionary<string, PropositionSet.Authored> Authored { get; }

    /// <summary>
    /// Every rule's state, indexed by the slot assigned at registration. Null only for a slot whose
    /// rule is mid-registration inside <see cref="RuleSet.Add"/>.
    /// </summary>
    public RuleSlot?[] RuleSlots { get; }

    /// <summary>Resolution in this world: authored first, then compiled.</summary>
    public ISpecSource Source { get; }
}
