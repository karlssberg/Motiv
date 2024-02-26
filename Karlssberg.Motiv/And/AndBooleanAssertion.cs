namespace Karlssberg.Motiv.And;

internal class AndBooleanAssertion<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IAssertion
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    public int CausalOperandCount => _causalResults.Length;

    public string Short => 
        CausalOperandCount switch
        {
            0 => "",
            1 => _causalResults.First().Assertion.Short,
            _ =>  string.Join(" & ", _causalResults.Select(ExplainReasons))
        };

    public string Detailed =>
        GetDetails();

    private string GetDetails()
    {
        var leftDetails = Explain(left);
        var rightDetails = Explain(right);

        var isBracketed = leftDetails.IsBracketed() || rightDetails.IsBracketed();
        var isTooLong = leftDetails.IsLongExpression() || rightDetails.IsLongExpression();
        if (isBracketed || isTooLong)
            return $"""
                    {leftDetails} &
                    {rightDetails}
                    """;
        
        return $"{leftDetails} & {rightDetails}";
    }

    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndAssertion<TMetadata> andSpec =>
                andSpec.Assertion.Detailed,
            ICompositeAssertion compositeSpec =>
                $"({compositeSpec.Assertion.Detailed})",
            _ => result.Assertion.Detailed
        };
    }
    
    private string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndAssertion<TMetadata> andSpec =>
                andSpec.Assertion.Short,
            ICompositeAssertion { Assertion.CausalOperandCount: > 1 } compositeSpec =>
                $"({compositeSpec.Assertion.Short})",
            _ => result.Assertion.Short
        };
    }
    
    public override string ToString() => Short;
}