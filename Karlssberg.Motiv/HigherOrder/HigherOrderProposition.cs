namespace Karlssberg.Motiv.HigherOrder;

internal class HigherOrderProposition<TModel, TUnderlyingMetadata>(
    string assertion, 
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : IProposition
{
    public string Assertion => assertion;
    public string Detailed => 
      $$"""
        {{assertion}} {
            {{underlyingSpec.Proposition.Detailed}}
        }
        """;
    
    public override string ToString() => Assertion;
}