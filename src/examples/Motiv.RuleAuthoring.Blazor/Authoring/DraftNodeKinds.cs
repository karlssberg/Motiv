namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>How each <see cref="DraftNodeKind" /> appears in a rule document.</summary>
public static class DraftNodeKinds
{
    /// <summary>The operator kinds, in the order the sample offers them.</summary>
    /// <remarks>Listed explicitly: this drives the editor's dropdown, so the order is a decision.</remarks>
    public static IReadOnlyList<DraftNodeKind> Operators { get; } =
    [
        DraftNodeKind.Not,
        DraftNodeKind.And,
        DraftNodeKind.Or,
        DraftNodeKind.XOr,
        DraftNodeKind.AndAlso,
        DraftNodeKind.OrElse
    ];

    /// <summary>The document keyword for an operator kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The keyword, e.g. <c>andAlso</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind" /> is not an operator.</exception>
    public static string Keyword(DraftNodeKind kind) => kind switch
    {
        DraftNodeKind.Not => "not",
        DraftNodeKind.And => "and",
        DraftNodeKind.Or => "or",
        DraftNodeKind.XOr => "xor",
        DraftNodeKind.AndAlso => "andAlso",
        DraftNodeKind.OrElse => "orElse",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an operator kind.")
    };

    /// <summary>Whether the kind takes exactly one operand, written as a nested node.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c> for <see cref="DraftNodeKind.Not" />.</returns>
    public static bool IsUnary(DraftNodeKind kind) => kind is DraftNodeKind.Not;

    /// <summary>Whether the kind takes <see cref="MinimumOperands" /> operands and no more.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c> for a spec node and a negation.</returns>
    /// <remarks>The n-ary operators have a minimum but no maximum, so they are not fixed.</remarks>
    public static bool IsFixedArity(DraftNodeKind kind) =>
        kind is DraftNodeKind.Spec or DraftNodeKind.Not;

    /// <summary>How many operands the kind needs before it can produce a valid document.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Zero for a spec, one for a negation, two for every other operator.</returns>
    /// <remarks>The two comes from the schema, whose <c>nodeArray</c> sets <c>minItems: 2</c>.</remarks>
    public static int MinimumOperands(DraftNodeKind kind) => kind switch
    {
        DraftNodeKind.Spec => 0,
        DraftNodeKind.Not => 1,
        _ => 2
    };
}
