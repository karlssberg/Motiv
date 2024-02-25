namespace Karlssberg.Motiv.And;

internal class AndBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IResultDescription
{
    public string Reason => string.Join(" & ", causalResults.Select(result => result.Description.Reason));

    public string Details =>
        $"""
           {Explain(left).IndentAfterFirstLine()}
         & {Explain(right).IndentAfterFirstLine()}
         """;
    
    
    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            AndBooleanResult<TMetadata> andSpec => andSpec.Description.Details,
            ICompositeBooleanResult compositeSpec => $"({compositeSpec.Description.Details})",
            _ => result.Description.Details
        };
    }
    
    public override string ToString() => Reason;
}