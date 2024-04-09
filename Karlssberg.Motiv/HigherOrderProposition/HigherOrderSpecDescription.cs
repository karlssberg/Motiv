namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderSpecDescription<TModel, TUnderlyingMetadata>(
    string statement,
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : ISpecDescription
{
    public string Statement => statement;

    public string Detailed =>
        $$"""
          {{statement}} {
              {{underlyingSpec.Description.Detailed}}
          }
          """;

    public override string ToString() => Statement;
}