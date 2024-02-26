using Humanizer;

namespace Karlssberg.Motiv;

public sealed class  CausalMetadata<TMetadata>(IEnumerable<TMetadata> metadata, IEnumerable<string> explanations)
{
    public CausalMetadata(TMetadata metadata, IAssertion description) : this([metadata], [description.Short])
    {
    }
    
    public IEnumerable<CausalMetadata<TMetadata>> Underlying { get; internal set; } = Enumerable.Empty<CausalMetadata<TMetadata>>();
    
    public IEnumerable<TMetadata> Metadata => metadata;

    public override string ToString() => explanations.Humanize();
}