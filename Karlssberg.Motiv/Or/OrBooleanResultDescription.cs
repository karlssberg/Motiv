namespace Karlssberg.Motiv.Or;

internal sealed class OrBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    
    internal override int CausalOperandCount => _causalResults.Length;
    
    public override string Reason => 
        CausalOperandCount switch
        {
            0 => "",
            1 => _causalResults.First().Description.Reason,
            _ =>  string.Join(" | ", _causalResults.Select(ExplainReasons))
        };

    public override string Detailed => GetDetails();

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
                orSpec.Description.Detailed,
            ICompositeBooleanResult compositeSpec =>
                $"({compositeSpec.Description.Detailed})",
            _ => result.Description.Detailed
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