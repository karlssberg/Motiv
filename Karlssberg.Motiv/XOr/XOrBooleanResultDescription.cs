namespace Karlssberg.Motiv.XOr;

internal class XOrBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IResultDescription
{
    public string Reason => string.Join(" ^ ", causalResults.Select(result => result.Description.Reason));

    public string Details =>
        $"""
           {Explain(left).IndentAfterFirstLine()}
         ^ {Explain(right).IndentAfterFirstLine()}
         """;
    
    
    private string Explain(BooleanResultBase<TMetadata> result)
    {
        return result switch 
        {
            XOrBooleanResult<TMetadata> xOrSpec => xOrSpec.Description.Details,
            ICompositeBooleanResult compositeSpec => $"({compositeSpec.Description.Details})",
            _ => result.Description.Details
        };
    }
    
    public override string ToString() => Reason;
}