namespace Karlssberg.Motiv.And;

internal class AndBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IResultDescription
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    public int CausalOperandCount => _causalResults.Length;

    public string Reason => 
        CausalOperandCount switch
        {
            0 => "",
            1 => _causalResults.First().Description.Reason,
            _ =>  string.Join(" & ", _causalResults.Select(ExplainReasons))
        };

    public string Details =>
        $"""
           {ExplainDetails(left).IndentAfterFirstLine()}
         & {ExplainDetails(right).IndentAfterFirstLine()}
         """;
    
    
    private string ExplainDetails(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndBooleanResult<TMetadata> andSpec => andSpec.Description.Details,
            ICompositeBooleanResult compositeSpec => $"({compositeSpec.Description.Details})",
            _ => result.Description.Details
        };
    }
    
    private string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndBooleanResult<TMetadata> andSpec => andSpec.Description.Reason,
            ICompositeBooleanResult { Description.CausalOperandCount: > 1 } compositeSpec => $"({compositeSpec.Description.Reason})",
            _ => result.Description.Reason
        };
    }
    
    public override string ToString() => Reason;
}