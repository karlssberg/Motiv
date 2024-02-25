namespace Karlssberg.Motiv.XOr;

internal class XOrBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IResultDescription
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    
    public int CausalOperandCount => _causalResults.Length;
    
    public string Reason => string.Join(" ^ ", _causalResults.Select(result => result.Description.Reason));

    public string Details => 
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
            XOrBooleanResult<TMetadata> xOrSpec => 
                xOrSpec.Description.Details,
            ICompositeBooleanResult compositeSpec => 
                $"({compositeSpec.Description.Details})",
            _ => result.Description.Details
        };
    }
    
    public override string ToString() => Reason;
}