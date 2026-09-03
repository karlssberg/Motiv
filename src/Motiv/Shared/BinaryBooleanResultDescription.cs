using Motiv.Traversal;

namespace Motiv.Shared;

internal abstract class BinaryBooleanResultDescription<TMetadata>(
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();

    internal override int CausalOperandCount => _causalResults.Length;

    public override string Reason => FoldedReason;

    private protected override IReadOnlyList<ResultDescriptionBase> ReasonOperands =>
        field ??= ReasonRun.Select(result => result.Description).ToArray();

    private protected override string ComposeReason(IReadOnlyList<string> operandReasons) =>
        ReasonRun.Count switch
        {
            0 => "",
            1 => operandReasons[0],
            _ => string.Join(Separator, Explained(operandReasons))
        };

    /// <summary>
    /// The operands this description's reason is joined from: a run of nested same-operation
    /// compositions flattened into one list, so that the whole run renders as one join rather than a
    /// level at a time.
    /// </summary>
    private IReadOnlyList<BooleanResultBase<TMetadata>> ReasonRun =>
        field ??= RunFlattener.Flatten(_causalResults, RunContinuedBy);

    /// <summary>
    /// The operands of <paramref name="operand" /> when this description's reason may be joined from
    /// them instead of from <paramref name="operand" /> itself, and <c>null</c> when the run stops
    /// there.
    /// </summary>
    /// <remarks>
    /// Both conditions are the same one: collapsing is sound exactly where the operand's reason is
    /// currently reproduced verbatim, so that replacing it with the operands it was joined from
    /// cannot change a character.
    /// <list type="bullet">
    /// <item>
    /// <b>The same statement.</b> Not the same <i>family</i>: <c>And</c>'s family admits
    /// <c>AndAlso</c>, whose reason is joined with <c>" &amp;&amp; "</c>, and collapsing one into the
    /// other would rewrite the separator. Statement and separator are in bijection across the four
    /// subclasses, so equal statements are the same class — hence the same separator and the same
    /// <see cref="ExplainReason" />. It is also the condition the justification's own collapse
    /// applies, which is why the two renderers now agree about where a run ends.
    /// </item>
    /// <item>
    /// <b>More than one causal operand.</b> A composition that contributed a single cause renders as
    /// that cause's reason verbatim — <see cref="ComposeReason" /> returns it without consulting
    /// <see cref="ExplainReason" />, so an equality assertion arrives unparenthesised. Collapsing it
    /// would promote the cause to an operand of this description, which does parenthesise one.
    /// </item>
    /// </list>
    /// </remarks>
    private IEnumerable<BooleanResultBase<TMetadata>>? RunContinuedBy(BooleanResultBase<TMetadata> operand) =>
        operand.Description is BinaryBooleanResultDescription<TMetadata> nested
        && nested.Statement == Statement
        && nested.CausalOperandCount > 1
            ? nested._causalResults
            : null;

    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        FoldedJustification(withoutCausalCount: true);

    private protected override IReadOnlyList<Rendering> JustificationOperands(bool withoutCausalCount) =>
        Collapsed.Select(result => new Rendering(result.Description, withoutCausalCount)).ToArray();

    private protected override string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        BinaryJustification(Statement, operandLines);

    /// <summary>
    /// The causal results as they are rendered: a run of nested same-operation compositions collapses
    /// into one group beneath a single conjunction heading.
    /// </summary>
    private IReadOnlyList<BooleanResultBase> Collapsed => field ??= _causalResults.FlattenCollapsible(Statement);

    protected abstract string Separator { get; }

    protected abstract bool IsSameFamily(BooleanResultBase<TMetadata> result);

    private IEnumerable<string> Explained(IReadOnlyList<string> operandReasons)
    {
        for (var i = 0; i < ReasonRun.Count; i++)
            yield return ExplainReason(ReasonRun[i], operandReasons[i]);
    }

    private string ExplainReason(BooleanResultBase<TMetadata> result, string reason)
    {
        return result switch
        {
            _ when IsSameFamily(result) => reason,
            _ when result.Causes.HasAtLeast(2) => $"({reason})",
            _ when reason.EndsWithEqualityAssertion() => $"({reason})",
            _ => reason
        };
    }
}
