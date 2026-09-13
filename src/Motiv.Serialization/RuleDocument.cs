namespace Motiv.Serialization;

internal sealed class RuleDocument(
    string? name,
    RuleNode? root,
    IReadOnlyList<RuleParameterDeclaration> parameters,
    bool audited = false,
    IReadOnlyList<RuleNode>? definitions = null)
{
    public string? Name { get; } = name;

    public RuleNode? Root { get; } = root;

    public IReadOnlyList<RuleParameterDeclaration> Parameters { get; } = parameters;

    /// <summary>
    /// The document-local propositions declared by <c>definitions</c>, in declaration order. Each
    /// node is a definition's body, carrying the definition key as its <see cref="RuleNode.Name" />
    /// and the definition's payloads, so binding one is binding any other decorated node.
    /// </summary>
    public IReadOnlyList<RuleNode> Definitions { get; } = definitions ?? [];

    /// <summary>
    /// Whether every evaluation of this rule is recorded to the decision log. On the document rather
    /// than in host configuration, so that the flag is versioned with the rule, toggling it is a
    /// governed change, and a rule running on a compiled default — which has no document — cannot
    /// claim to be audited.
    /// </summary>
    public bool Audited { get; } = audited;
}
