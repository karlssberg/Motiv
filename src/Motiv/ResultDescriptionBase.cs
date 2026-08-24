using Motiv.Shared;
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

    /// <summary>One description rendered in one of the two justification modes.</summary>
    private protected readonly struct Rendering(ResultDescriptionBase description, bool withoutCausalCount)
    {
        public ResultDescriptionBase Description { get; } = description;

        public bool WithoutCausalCount { get; } = withoutCausalCount;
    }

    /// <summary>
    /// Renders this description and everything beneath it, deepest first, so that a formatter builds
    /// its lines from its operands' already-rendered blocks rather than by calling into them.
    /// </summary>
    /// <remarks>
    /// The memo is walk-local rather than a field on the node, because a description renders
    /// differently in the two modes and both may be wanted — the same reason there are two methods.
    /// </remarks>
    private protected string[] FoldedJustification(bool withoutCausalCount)
    {
        var memo = new Dictionary<Rendering, string[]>(RenderingComparer.Instance);

        return PostOrderFold.Fold(
            new Rendering(this, withoutCausalCount),
            RenderingOperands,
            ComposeRendering,
            rendering => memo.TryGetValue(rendering, out var lines) ? lines : null,
            (rendering, lines) => memo[rendering] = lines);
    }

    /// <summary>The renderings this description's own lines are built from, in order.</summary>
    private protected virtual IReadOnlyList<Rendering> JustificationOperands(bool withoutCausalCount) => [];

    /// <summary>
    /// Builds this description's lines from its operands', supplied in
    /// <see cref="JustificationOperands" /> order. The default is for a formatter that recurses into
    /// nothing.
    /// </summary>
    private protected virtual string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        (withoutCausalCount ? GetJustificationAsLinesWithoutCausalCount() : GetJustificationAsLines()).ToArray();

    /// <summary>Prefixes a conjunction heading to its operands' lines, indented beneath it.</summary>
    private protected static string[] BinaryJustification(string conjunction, IReadOnlyList<string[]> operandLines)
    {
        var lines = new List<string> { conjunction };

        for (var i = 0; i < operandLines.Count; i++)
            foreach (var line in operandLines[i])
                lines.Add(line.Indent());

        return lines.ToArray();
    }

    private static readonly Func<Rendering, IReadOnlyList<Rendering>> RenderingOperands =
        rendering => rendering.Description.JustificationOperands(rendering.WithoutCausalCount);

    private static readonly Func<Rendering, IReadOnlyList<string[]>, string[]> ComposeRendering =
        (rendering, operandLines) =>
            rendering.Description.ComposeJustification(operandLines, rendering.WithoutCausalCount);

    private sealed class RenderingComparer : IEqualityComparer<Rendering>
    {
        public static readonly RenderingComparer Instance = new();

        public bool Equals(Rendering x, Rendering y) =>
            ReferenceEquals(x.Description, y.Description) && x.WithoutCausalCount == y.WithoutCausalCount;

        public int GetHashCode(Rendering rendering) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rendering.Description) * 2
            + (rendering.WithoutCausalCount ? 1 : 0);
    }
}
