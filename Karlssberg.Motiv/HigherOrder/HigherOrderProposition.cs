namespace Karlssberg.Motiv.HigherOrder;

internal class HigherOrderProposition<TModel, TUnderlyingMetadata>(
    string name, 
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : IProposition
{
    public string Assertion => name;
    public string Detailed => 
      $$"""
        {{name}} {
            {{underlyingSpec.Proposition.Detailed}}
        }
        """;
    
    public override string ToString() => Assertion;
}