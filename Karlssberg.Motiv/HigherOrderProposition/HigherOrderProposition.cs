namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderProposition<TModel, TUnderlyingMetadata>(
    string statement,
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : IProposition
{
    public string Statement => statement;

    public string Detailed =>
        $$"""
          {{statement}} {
              {{underlyingSpec.Proposition.Detailed}}
          }
          """;

    public override string ToString() => Statement;
}