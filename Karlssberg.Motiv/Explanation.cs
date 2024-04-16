using System.Diagnostics;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{GetDebuggerDisplay()}")]
public sealed class Explanation
{
    private readonly Lazy<IEnumerable<string>> _lazyDistinctAssertions;
    private readonly Lazy<IEnumerable<string>> _lazyDistinctAllAssertions;

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
        _lazyDistinctAllAssertions = new Lazy<IEnumerable<string>>(assertions.Distinct);
    }

    public Explanation(IEnumerable<string> assertions, IEnumerable<string> allAssertions)
    {
        _lazyDistinctAssertions = new Lazy<IEnumerable<string>>(assertions.Distinct);
        _lazyDistinctAllAssertions = new Lazy<IEnumerable<string>>(allAssertions.Distinct);
    }

    public IEnumerable<string> Assertions => _lazyDistinctAssertions.Value;
    
    public IEnumerable<string> AllAssertions => _lazyDistinctAllAssertions.Value;
    
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