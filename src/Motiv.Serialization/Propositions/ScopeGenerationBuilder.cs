namespace Motiv.Serialization;

/// <summary>
/// The only way to produce a <see cref="ScopeGeneration"/>. A builder forks the world it starts
/// from, is written into freely, and yields one successor — so a caller cannot publish half a change,
/// and "everything fallible runs before anything mutates" has somewhere to put the not-yet-mutated
/// state.
/// </summary>
/// <remarks>
/// Not thread-safe, and not meant to be: a builder is created, written and built inside one holder of
/// <see cref="BindingScope"/>'s inner monitor, or off to the side by a refresh that owns it alone.
/// </remarks>
internal sealed class ScopeGenerationBuilder
{
    private readonly SpecRegistry _registry;
    private readonly PropositionOverlay _overlay;
    private readonly Dictionary<NodeId, IRebindable> _participants;
    private readonly Dictionary<string, PropositionSet.Authored> _authored;
    private RuleSlot?[] _ruleSlots;
    private StoreGeneration _sequence;

    /// <summary>Forks an existing world — the publish path, which changes a little and keeps the rest.</summary>
    public ScopeGenerationBuilder(SpecRegistry registry, ScopeGeneration from)
    {
        _registry = registry;
        _overlay = new PropositionOverlay(from.Overlay);
        Graph = new DependencyGraph(from.Graph);
        // Copied entry by entry rather than through a copy constructor: the source is an
        // IReadOnlyDictionary, and netstandard2.0's Dictionary has no constructor that takes one.
        _participants = [];
        foreach (var entry in from.Participants)
            _participants[entry.Key] = entry.Value;

        _authored = new Dictionary<string, PropositionSet.Authored>(StringComparer.Ordinal);
        foreach (var entry in from.Authored)
            _authored[entry.Key] = entry.Value;

        _ruleSlots = [.. from.RuleSlots];
        _sequence = from.Sequence;
    }

    /// <summary>
    /// Starts from nothing but the rules' shape — the refresh path, which rebuilds the authored world
    /// from the store rather than amending it. Slots are carried over in count only: a refresh rebinds
    /// every rule, and a slot index is stable for a rule's lifetime.
    /// </summary>
    public ScopeGenerationBuilder(SpecRegistry registry, int ruleCount)
    {
        _registry = registry;
        _overlay = new PropositionOverlay();
        Graph = new DependencyGraph();
        _participants = [];
        _authored = new Dictionary<string, PropositionSet.Authored>(StringComparer.Ordinal);
        _ruleSlots = new RuleSlot?[ruleCount];
        _sequence = StoreGeneration.Zero;
    }

    /// <summary>The prospective graph, written directly by the callers that track edges.</summary>
    public DependencyGraph Graph { get; }

    /// <summary>Resolution as this prospective world would resolve — what a rebind binds against.</summary>
    public ISpecSource Source => new LayeredSpecSource(_overlay, _registry);

    public void SetOverlayEntry(SpecRegistryEntry entry) => _overlay.Set(entry);

    public void RemoveOverlayEntry(string name) => _overlay.Remove(name);

    public void SetAuthored(PropositionSet.Authored authored) => _authored[authored.Name] = authored;

    public void RemoveAuthored(string name) => _authored.Remove(name);

    public PropositionSet.Authored? FindAuthored(string name) =>
        _authored.TryGetValue(name, out var authored) ? authored : null;

    public void Enrol(IRebindable participant) => _participants[participant.Node] = participant;

    public void Withdraw(NodeId node) => _participants.Remove(node);

    /// <summary>Grows the slot array so <paramref name="count"/> rules fit. Never shrinks: slots are permanent.</summary>
    public void EnsureRuleSlots(int count)
    {
        if (_ruleSlots.Length >= count)
            return;

        var grown = new RuleSlot?[count];
        Array.Copy(_ruleSlots, grown, _ruleSlots.Length);
        _ruleSlots = grown;
    }

    /// <summary>Publishes a rule's binding, clearing any quarantine — see <see cref="RuleSlot.WithState"/>.</summary>
    public void SetRuleState(int slot, object state)
    {
        EnsureRuleSlots(slot + 1);
        _ruleSlots[slot] = _ruleSlots[slot] is { } existing
            ? existing.WithState(state)
            : new RuleSlot(state, []);
    }

    /// <summary>Records why a stored document could not be applied, keeping the binding in place.</summary>
    public void SetRuleQuarantine(int slot, IReadOnlyList<RuleError> quarantine)
    {
        EnsureRuleSlots(slot + 1);
        _ruleSlots[slot] = _ruleSlots[slot] is { } existing
            ? existing.WithQuarantine(quarantine)
            : throw new InvalidOperationException(
                $"Rule slot {slot} has no state, so there is nothing to quarantine.");
    }

    public void SetSequence(StoreGeneration sequence) => _sequence = sequence;

    public ScopeGeneration Build() =>
        new(_registry, _sequence, _overlay, Graph, _participants, _authored, _ruleSlots);
}
