namespace Karlssberg.Motiv;

internal sealed class Proposition(string statement, IProposition? underlyingProposition = null) : IProposition
{
    public string Statement => statement;

    public string Detailed =>
        underlyingProposition switch
        {
            null => statement,
            not null =>
                $$"""
                  {{statement}} {
                      {{underlyingProposition.Detailed.IndentAfterFirstLine()}}
                  }
                  """
        };

    public override string ToString() => Statement;
}