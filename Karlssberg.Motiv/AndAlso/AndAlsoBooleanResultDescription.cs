using Karlssberg.Motiv.And;

namespace Karlssberg.Motiv.AndAlso;

internal sealed class AndAlsoBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right,
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

    public override string Detailed => GetDetails();

    private string GetDetails()
    {
        var leftDetails = Explain(left);
        
        if (right is null)
            return leftDetails;
        
        var rightDetails = Explain(right);

        var isBracketed = leftDetails.IsBracketed() || rightDetails.IsBracketed();
        var isTooLong = leftDetails.IsLongExpression() || rightDetails.IsLongExpression();
        if (isBracketed || isTooLong)
            return $"""
                    {leftDetails} &&
                    {rightDetails}
                    """;
        
        return $"{leftDetails} && {rightDetails}";
    }

    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndBooleanResult<TMetadata> andSpec =>
                andSpec.Description.Detailed,
            AndAlsoBooleanResult<TMetadata> andAlsoSpec =>
                andAlsoSpec.Description.Detailed,
            ICompositeBooleanResult compositeSpec =>
                $"({compositeSpec.Description.Detailed})",
            _ => result.Description.Detailed
        };
    }
    
    private string ExplainReasons(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndBooleanResult<TMetadata> andSpec =>
                andSpec.Description.Reason,
            AndAlsoBooleanResult<TMetadata> andAlsoSpec =>
                andAlsoSpec.Description.Reason,
            ICompositeBooleanResult { Description.CausalOperandCount: > 1 } compositeSpec =>
                $"({compositeSpec.Description.Reason})",
            _ => result.Description.Reason
        };
    }
}