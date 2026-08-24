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

    public override IEnumerable<string> GetJustificationAsLines() =>
        _causalResults.GetBinaryJustificationAsLines(Statement);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        _causalResults.GetBinaryJustificationAsLines(Statement, withoutCausalCount: true);

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
