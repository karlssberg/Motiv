namespace Karlssberg.Motiv.ElseIf;

internal sealed class ElseIfProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> antecedent,
    SpecBase<TModel, TMetadata> consequent)
    : IProposition
{
    public string Assertion => $"{antecedent.Proposition.Assertion} => {consequent.Proposition.Assertion}";
    public string Detailed =>
        $"""
        {antecedent.Proposition.Detailed} =>
            {consequent.Proposition.Detailed.IndentAfterFirstLine()}
        """;
    
    public override string ToString() => Assertion;
}