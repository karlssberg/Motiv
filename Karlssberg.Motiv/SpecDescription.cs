namespace Karlssberg.Motiv;

internal sealed class SpecDescription(string statement, string? underlyingDetailedDescription = null) : ISpecDescription
{
    public string Statement => statement;

    public string Detailed =>
        underlyingDetailedDescription switch
        {
            null => statement,
            not null =>
                $$"""
                  {{statement}} {
                      {{underlyingDetailedDescription.IndentAfterFirstLine()}}
                  }
                  """
        };

    public override string ToString() => Statement;
}