namespace Karlssberg.Motiv;

internal sealed class Proposition(string assertion, IProposition? underlyingProposition = null) : IProposition
{
    public string Assertion => assertion;

    public string Detailed =>
        underlyingProposition switch
        {
            null => assertion,
            not null =>
                $$"""
                  {{assertion}} {
                      {{underlyingProposition.Detailed.IndentAfterFirstLine()}}
                  }
                  """
        };

    public override string ToString() => Assertion;
}