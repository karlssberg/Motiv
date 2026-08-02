namespace Motiv.Serialization;

/// <summary>What kind of thing a dependency-graph node is.</summary>
internal enum NodeKind
{
    /// <summary>An authored proposition. Referenceable, so it can be an edge target.</summary>
    Proposition,

    /// <summary>A live rule. Documents reference specs and never rules, so a rule is always a sink.</summary>
    Rule
}

/// <summary>
/// Identifies a node in the dependency graph. Kind is part of the identity because nothing stops a
/// host naming a rule after a proposition, and merging the two would corrupt the closure.
/// </summary>
internal readonly record struct NodeId(NodeKind Kind, string Name)
{
    /// <summary>
    /// How the kind is spelled on the wire, in <c>PropositionDependent.Kind</c> and
    /// <c>BrokenDependent.Kind</c>. One producer, because it is one contract: clients switch on
    /// these strings, so two independent copies of the mapping is one copy too many.
    /// </summary>
    public string KindLabel => Kind == NodeKind.Rule ? "rule" : "proposition";

    public static NodeId Proposition(string name) => new(NodeKind.Proposition, name);

    public static NodeId Rule(string name) => new(NodeKind.Rule, name);

    public override string ToString() => $"{Kind}:{Name}";
}
