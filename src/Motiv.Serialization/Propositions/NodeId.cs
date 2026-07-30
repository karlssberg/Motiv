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
    public static NodeId Proposition(string name) => new(NodeKind.Proposition, name);

    public static NodeId Rule(string name) => new(NodeKind.Rule, name);

    public override string ToString() => $"{Kind}:{Name}";
}
