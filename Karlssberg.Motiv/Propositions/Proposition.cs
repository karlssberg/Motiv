namespace Karlssberg.Motiv.Propositions;

internal class Proposition(string name, IProposition? underlyingProposition = null) : IProposition
{
    public string Assertion => name;

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


    public override string ToString() => Assertion;
}