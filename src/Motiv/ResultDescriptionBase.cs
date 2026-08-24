using Motiv.Traversal;

namespace Motiv;

/// <summary>
/// Represents the base class for a description of a <see cref="BooleanResultBase"/>.
/// </summary>
public abstract class ResultDescriptionBase
{
    internal abstract int CausalOperandCount { get; }

    internal abstract string Statement { get; }

    /// <summary>
    /// Gets the reason for the result.
    /// </summary>
    public abstract string Reason { get; }

    /// <summary>
    /// Gets the multi-line detailed breakdown of the causes as a human-readable string.
    /// </summary>
    public virtual string Justification => field ??= string.Join(Environment.NewLine, GetJustificationAsLines());

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => Reason;

    /// <summary>
    /// Retrieves the details of the result as a collection of lines.
    /// </summary>
    /// <returns>An enumerable collection of strings, each representing a line of detail.</returns>
    public abstract IEnumerable<string> GetJustificationAsLines();

    internal virtual IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() => GetJustificationAsLines();

    /// <summary>
    /// A composed reason, built from its operands' reasons iteratively. A description whose reason is
    /// a plain string does not need this; the three that compose one (binary, exclusive-or and
    /// negation) return it from their <see cref="Reason" />.
    /// </summary>
    private protected string FoldedReason =>
        _foldedReason ??= PostOrderFold.Fold(this, OperandsOf, Compose, ReadReason, WriteReason);

    private string? _foldedReason;

    /// <summary>The descriptions whose reasons this description's reason is composed from, in order.</summary>
    private protected virtual IReadOnlyList<ResultDescriptionBase> ReasonOperands => [];

    /// <summary>
    /// Builds this description's reason from its operands', supplied in <see cref="ReasonOperands" />
    /// order. The default is for a description whose reason stands alone.
    /// </summary>
    private protected virtual string ComposeReason(IReadOnlyList<string> operandReasons) => Reason;

    private static readonly Func<ResultDescriptionBase, IReadOnlyList<ResultDescriptionBase>> OperandsOf =
        description => description.ReasonOperands;

    private static readonly Func<ResultDescriptionBase, IReadOnlyList<string>, string> Compose =
        (description, operandReasons) => description.ComposeReason(operandReasons);

    private static readonly Func<ResultDescriptionBase, string?> ReadReason =
        description => description._foldedReason;

    private static readonly Action<ResultDescriptionBase, string> WriteReason =
        (description, reason) => description._foldedReason = reason;
}
