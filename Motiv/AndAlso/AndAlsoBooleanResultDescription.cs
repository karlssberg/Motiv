using Motiv.And;

namespace Motiv.AndAlso;

internal sealed class AndAlsoBooleanResultDescription<TMetadata>(
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
            _ =>  string.Join(" && ", causalResults.Select(ExplainReasons))
        };

    public override IEnumerable<string> GetJustificationAsLines() =>
        causalResults.GetBinaryJustificationAsLines(operationName);
    
    private static string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndBooleanResult<TMetadata> andSpec =>
                andSpec.Description.Reason,
            AndAlsoBooleanResult<TMetadata> andAlsoSpec =>
                andAlsoSpec.Description.Reason,
            _ when result.UnderlyingAssertionSources.Count() > 1 =>
                $"({result.Description.Reason})",
            _ => result.Description.Reason
        };
    }
}