﻿namespace Karlssberg.Motiv.And;

internal sealed class AndBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    internal override int CausalOperandCount => _causalResults.Length;

    public override string Compact => 
        CausalOperandCount switch
        {
            0 => "",
            1 => _causalResults.First().Description.Compact,
            _ =>  string.Join(" & ", _causalResults.Select(ExplainReasons))
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
                    {leftDetails} &
                    {rightDetails}
                    """;
        
        return $"{leftDetails} & {rightDetails}";
    }

    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndBooleanResult<TMetadata> andSpec =>
                andSpec.Description.Detailed,
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
                andSpec.Description.Compact,
            ICompositeBooleanResult { Description.CausalOperandCount: > 1 } compositeSpec =>
                $"({compositeSpec.Description.Compact})",
            _ => result.Description.Compact
        };
    }
}