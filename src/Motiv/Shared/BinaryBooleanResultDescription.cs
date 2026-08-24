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
        field ??= Array.ConvertAll(_causalResults, result => result.Description);

    private protected override string ComposeReason(IReadOnlyList<string> operandReasons) =>
        CausalOperandCount switch
        {
            0 => "",
            1 => operandReasons[0],
            _ => string.Join(Separator, Explained(operandReasons))
        };

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
        for (var i = 0; i < _causalResults.Length; i++)
            yield return ExplainReason(_causalResults[i], operandReasons[i]);
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
