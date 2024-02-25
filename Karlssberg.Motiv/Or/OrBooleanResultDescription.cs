namespace Karlssberg.Motiv.Or;

internal class OrBooleanResultDescription<TMetadata>(
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
            _ =>  string.Join(" | ", _causalResults.Select(ExplainReasons))
        };

    public string Details => GetDetails();

    private string GetDetails()
    {
        var leftDetails = Explain(left);
        var rightDetails = Explain(right);
        
        var isBracketed = leftDetails.IsBracketed() || rightDetails.IsBracketed();
        var isTooLong = leftDetails.IsLongExpression() || rightDetails.IsLongExpression();
        if (isBracketed || isTooLong)
            return $"""
                    {rightDetails} |
                    {leftDetails}
                    """;
        
        return $"{leftDetails} | {rightDetails}";
    }

    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            OrBooleanResult<TMetadata> orSpec => 
                orSpec.Description.Details,
            ICompositeBooleanResult compositeSpec =>
                $"({compositeSpec.Description.Details})",
            _ => result.Description.Details
        };
    }
    
    private string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            OrBooleanResult<TMetadata> orSpec => 
                orSpec.Description.Reason,
            ICompositeBooleanResult { Description.CausalOperandCount: > 1 } compositeSpec =>
                $"({compositeSpec.Description.Reason})",
            _ => result.Description.Reason
        };
    }
    
    public override string ToString() => Reason;
}