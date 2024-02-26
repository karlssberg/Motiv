namespace Karlssberg.Motiv.XOr;

internal class XOrBooleanAssertion<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IAssertion
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    
    public int CausalOperandCount => _causalResults.Length;
    
    public string Short => string.Join(" ^ ", _causalResults.Select(result => result.Assertion.Short));

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
                    {leftDetails} ^
                    {rightDetails}
                    """;
        
        return $"{leftDetails} ^ {rightDetails}";
    }


    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            XOrAssertion<TMetadata> xOrSpec => 
                xOrSpec.Assertion.Detailed,
            ICompositeAssertion compositeSpec => 
                $"({compositeSpec.Assertion.Detailed})",
            _ => result.Assertion.Detailed
        };
    }
    
    public override string ToString() => Short;
}