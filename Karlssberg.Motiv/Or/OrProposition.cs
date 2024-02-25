namespace Karlssberg.Motiv.Or;

internal class OrProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left, 
    SpecBase<TModel, TMetadata> right)
    : IProposition
{

    public string Name => $"{Summarize(left)} | {Summarize(right)}";
    public string Detailed =>
        $"""
           {Explain(left).IndentAfterFirstLine()}
         | {Explain(right).IndentAfterFirstLine()}
         """;
    
    private string Summarize(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch 
        {
            OrSpec<TModel, TMetadata> andSpec => andSpec.Proposition.Name,
            ICompositeSpec compositeSpec => $"({compositeSpec.Proposition.Name})",
            _ => operand.Proposition.Name
        };
    }
    
    private string Explain(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch 
        {
            OrSpec<TModel, TMetadata> andSpec => andSpec.Proposition.Detailed,
            ICompositeSpec compositeSpec => $"({compositeSpec.Proposition.Detailed})",
            _ => operand.Proposition.Detailed
        };
    }
    
    public override string ToString() => Name;
}