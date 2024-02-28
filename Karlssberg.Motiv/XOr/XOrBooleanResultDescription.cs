namespace Karlssberg.Motiv.XOr;

internal sealed class XOrBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    
    internal override int CausalOperandCount => _causalResults.Length;
    
    public override string Compact => string.Join(" ^ ", _causalResults.Select(result => result.Description.Compact));

    public override string Detailed => GetDetails();

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
                xOrSpec.Description.Detailed,
            ICompositeBooleanResult compositeSpec => 
                $"({compositeSpec.Description.Detailed})",
            _ => result.Description.Detailed
        };
    }
    
    public override string ToString() => Compact;
}