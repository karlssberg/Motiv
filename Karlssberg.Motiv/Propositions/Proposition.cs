namespace Karlssberg.Motiv.Propositions;

internal class Proposition(string name, IProposition? underlyingProposition = null) : IProposition
{
    public string Name => name;

    public string Detailed =>
        underlyingProposition switch
        {
            null => name,
            not null => $$"""
                          {{name}} {
                            {{underlyingProposition.Detailed.IndentAfterFirstLine()}}
                          }
                          """
        };


    public override string ToString() => Name;
}