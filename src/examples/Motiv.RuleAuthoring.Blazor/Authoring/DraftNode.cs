namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>
/// One node of the sample's own authoring tree.
/// </summary>
/// <remarks>
/// Motiv.Serialization's document model (<c>RuleDocument</c>, <c>RuleNode</c>) is internal, so a
/// .NET authoring UI brings its own. That is the boundary documented in
/// <c>docs/adoption/index.md</c>, and this type is what living inside it costs.
/// </remarks>
public sealed class DraftNode
{
    private DraftNode(DraftNodeKind kind) => Kind = kind;

    /// <summary>What this node composes.</summary>
    public DraftNodeKind Kind { get; private set; }

    /// <summary>The registry name a <see cref="DraftNodeKind.Spec" /> node references.</summary>
    /// <remarks>Empty until the author chooses one, which <c>Validate</c> reports and the editor shows.</remarks>
    public string SpecName { get; set; } = "";

    /// <summary>The operands of an operator node. Empty for a spec node.</summary>
    public List<DraftNode> Children { get; } = [];

    /// <summary>Creates a node referencing a registered proposition by name.</summary>
    /// <param name="specName">The registry name.</param>
    /// <returns>The node.</returns>
    public static DraftNode Spec(string specName) =>
        new(DraftNodeKind.Spec) { SpecName = specName };

    /// <summary>Creates an operator node over the given operands.</summary>
    /// <param name="kind">The operator kind.</param>
    /// <param name="operands">The operands.</param>
    /// <returns>The node.</returns>
    public static DraftNode Operator(DraftNodeKind kind, params DraftNode[] operands)
    {
        var node = new DraftNode(kind);
        node.Children.AddRange(operands);
        return node;
    }

    /// <summary>Changes what this node composes, keeping the operands that still apply.</summary>
    /// <param name="kind">The kind to become.</param>
    /// <remarks>
    /// Tops up to <see cref="DraftNodeKinds.MinimumOperands" />, so the editor cannot leave the
    /// author in a shape no valid document can be written from.
    /// </remarks>
    public void ChangeKindTo(DraftNodeKind kind)
    {
        Kind = kind;

        var minimum = DraftNodeKinds.MinimumOperands(kind);

        // Both halves are load-bearing: a fixed-arity kind sheds the operands it can no longer hold,
        // and the count guard covers the under-full case — a spec becoming `not` has fewer children
        // than the minimum, and the top-up below is what handles that.
        if (DraftNodeKinds.IsFixedArity(kind) && Children.Count > minimum)
            Children.RemoveRange(minimum, Children.Count - minimum);

        while (Children.Count < minimum)
            AddOperand();
    }

    /// <summary>Appends an operand whose proposition the author has yet to choose.</summary>
    public void AddOperand() => Children.Add(Spec(""));

    /// <summary>Removes an operand, unless this node still needs it.</summary>
    /// <param name="operand">The operand to remove.</param>
    /// <returns><c>true</c> if it was removed.</returns>
    public bool RemoveOperand(DraftNode operand) =>
        Children.Count > DraftNodeKinds.MinimumOperands(Kind) && Children.Remove(operand);
}
