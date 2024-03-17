namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderProposition<TModel, TUnderlyingMetadata>(
    string assertion,
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : IProposition
{
    public string Statement => assertion;

    public string Detailed =>
        $$"""
          {{assertion}} {
              {{underlyingSpec.Proposition.Detailed}}
          }
          """;

    public override string ToString() => Statement;
}