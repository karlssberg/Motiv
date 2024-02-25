namespace Karlssberg.Motiv.Or;

internal class OrBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IResultDescription
{
    public string Reason => string.Join(" | ", causalResults.Select(result => result.Description.Reason));

    public string Details =>
        $"""
           {Explain(left).IndentAfterFirstLine()}
         | {Explain(right).IndentAfterFirstLine()}
         """;
    
    
    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            OrBooleanResult<TMetadata> orSpec => orSpec.Description.Details,
            ICompositeBooleanResult compositeSpec => $"({compositeSpec.Description.Details})",
            _ => result.Description.Details
        };
    }
    
    public override string ToString() => Reason;
}