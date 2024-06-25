using Motiv.OrElse;

namespace Motiv.Or;

internal sealed class OrBooleanResultDescription<TMetadata>(
    string operationName,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : ResultDescriptionBase
{

    internal override int CausalOperandCount => causalResults.Count();

    public override string Reason =>
        CausalOperandCount switch
        {
            0 => "",
            1 => causalResults.First().Description.Reason,
            _ =>  string.Join(" | ", causalResults.Select(ExplainReasons))
        };

    public override IEnumerable<string> GetJustificationAsLines() =>
        causalResults.GetBinaryJustificationAsLines(operationName);

    private static string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch
        {
            OrBooleanResult<TMetadata> orResult =>
                orResult.Description.Reason,
            OrElseBooleanResult<TMetadata> orElseResult =>
                orElseResult.Description.Reason,
            _ when result.Causes.Count() > 1 =>
                $"({result.Description.Reason})",
            _ => result.Description.Reason
        };
    }

    public override string ToString() => Reason;
}
