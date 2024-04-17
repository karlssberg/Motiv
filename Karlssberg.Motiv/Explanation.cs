using System.Diagnostics;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{GetDebuggerDisplay()}")]
public sealed class Explanation
{
    private readonly Lazy<IEnumerable<string>> _lazyDistinctAssertions;

    public Explanation(string assertion) 
        : this(assertion.ToEnumerable())
    {
    }

    public Explanation(IEnumerable<string> assertions) : this(assertions.ToArray())
    {
    }
    
    public Explanation(ICollection<string> assertions) 
    {
        _lazyDistinctAssertions = new Lazy<IEnumerable<string>>(assertions.Distinct);
    }

    public IEnumerable<string> Assertions => _lazyDistinctAssertions.Value;
    
    public IEnumerable<Explanation> Underlying { get; internal set; } = Enumerable.Empty<Explanation>();
    
    public override string ToString() => Assertions.Serialize();

    private string GetDebuggerDisplay()
    {
        return HaveComprehensiveAssertions() || !Underlying.Any()
            ? ToString()
            : $$"""
                {{ToString()}} { {{Underlying.GetAssertions().Serialize()}} }
                """;
        
        bool HaveComprehensiveAssertions() => Assertions.HasAtLeast(2);
    }
}