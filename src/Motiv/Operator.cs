namespace Motiv;

/// <summary>
/// Boolean operations supported by Motiv
/// </summary>
public static class Operator
{
    /// <summary>
    /// The conjunctive boolean operation.
    /// </summary>
    public const string And = "AND";

    /// <summary>
    /// The short-circuiting conjunctive boolean operation.
    /// </summary>
    public const string AndAlso = "AND ALSO";

    /// <summary>
    /// The disjunctive boolean operation.
    /// </summary>
    public const string Or = "OR";

    /// <summary>
    /// The short-circuiting disjunctive boolean operation.
    /// </summary>
    public const string OrElse = "OR ELSE";

    /// <summary>
    /// The XOR logical operator.
    /// </summary>
    public const string XOr = "XOR";

    /// <summary>
    /// Logical negation.
    /// </summary>
    public const string Not = "NOT";
}
