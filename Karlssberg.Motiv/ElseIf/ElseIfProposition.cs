namespace Karlssberg.Motiv.ElseIf;

internal class ElseIfProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> antecedent,
    SpecBase<TModel, TMetadata> consequent)
    : IProposition
{
    public string Name => $"{antecedent.Proposition.Name} => {consequent.Proposition.Name}";
    public string Detailed =>
        $"""
         {antecedent.Proposition.Detailed} =>
           {consequent.Proposition.Detailed.IndentAfterFirstLine()}
         """;
    
    public override string ToString() => Name;
}