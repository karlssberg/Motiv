using Motiv.And;

namespace Motiv.AndAlso;

internal sealed class AndAlsoBooleanResultDescription<TMetadata>(
    string operationName,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => causalResults.Count();

    internal override string Statement => Operator.AndAlso;

    public override string Reason =>
        CausalOperandCount switch
        {
            0 => "",
            1 => causalResults.First().Description.Reason,
            _ =>  string.Join(" && ", causalResults.Select(ExplainReasons))
        };

    public override IEnumerable<string> GetJustificationAsLines() =>
        causalResults.GetBinaryJustificationAsLines(operationName);

    private static string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch
        {
            AndBooleanResult<TMetadata> andResult =>
                andResult.Description.Reason,
            AndAlsoBooleanResult<TMetadata> andAlsoResult =>
                andAlsoResult.Description.Reason,
            _ when result.Causes.Count() > 1 =>
                $"({result.Description.Reason})",
            _ when result.Description.Reason.EndsWithEqualityAssertion() =>
                $"({result.Description.Reason})",
            _ => result.Description.Reason
        };
    }
}
