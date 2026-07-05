namespace Motiv.Shared;

internal abstract class BinaryBooleanResultDescription<TMetadata>(
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();

    internal override int CausalOperandCount => _causalResults.Length;

    public override string Reason =>
        field ??= CausalOperandCount switch
        {
            0 => "",
            1 => _causalResults[0].Description.Reason,
            _ => string.Join(Separator, _causalResults.Select(ExplainReasons))
        };

    public override IEnumerable<string> GetJustificationAsLines() =>
        _causalResults.GetBinaryJustificationAsLines(Statement);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        _causalResults.GetBinaryJustificationAsLines(Statement, withoutCausalCount: true);

    protected abstract string Separator { get; }

    protected abstract bool IsSameFamily(BooleanResultBase<TMetadata> result);

    private string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch
        {
            _ when IsSameFamily(result) =>
                result.Description.Reason,
            _ when result.Causes.HasAtLeast(2) =>
                $"({result.Description.Reason})",
            _ when result.Description.Reason.EndsWithEqualityAssertion() =>
                $"({result.Description.Reason})",
            _ => result.Description.Reason
        };
    }
}
