namespace Karlssberg.Motiv.Or;

internal class OrBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right,
    IEnumerable<BooleanResultBase<TMetadata>> causalResults) 
    : IResultDescription
{
    private readonly BooleanResultBase<TMetadata>[] _causalResults = causalResults.ToArray();
    
    public int CausalOperandCount => _causalResults.Length;
    
    public string Reason => string.Join(" | ", _causalResults.Select(result => result.Description.Reason));

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