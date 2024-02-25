namespace Karlssberg.Motiv.HigherOrder;

internal class HigherOrderProposition<TModel, TUnderlyingMetadata>(
    string name, 
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : IProposition
{
    public string Name => name;
    public string Detailed => 
      $$"""
        {{name}} {
            {{underlyingSpec.Proposition.Detailed}}
        }
        """;
    
    public override string ToString() => Name;
}