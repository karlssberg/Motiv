using System.Diagnostics;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{GetDebuggerDisplay()}")]
public sealed class Explanation(IEnumerable<string> assertions)
{
    public Explanation(string assertion) 
        : this(assertion.ToEnumerable())
    {
    }
    
    public Explanation(ResultDescriptionBase resultDescription) 
        : this(resultDescription.Compact.ToEnumerable())
    {
    }
 
    public IEnumerable<string> Assertions => assertions;
    
    public IEnumerable<Explanation> Underlying { get; internal set; } = Enumerable.Empty<Explanation>();
    
    public override string ToString() => string.Join(", ", Assertions);

    private string GetDebuggerDisplay()
    {
        return HaveComprehensiveAssertions() || !Underlying.Any()
            ? ToString()
            : $$"""
                {{ToString()}} { {{string.Join(", ", Underlying.GetAssertions())}} }
                """;
        
        bool HaveComprehensiveAssertions() => Assertions.HasAtLeast(2);
    }
}