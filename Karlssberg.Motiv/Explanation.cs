using System.Diagnostics;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{GetDebuggerDisplay()}")]
public sealed class Explanation(IEnumerable<string> assertions)
{
    public Explanation(ResultDescriptionBase resultDescription) 
        : this(resultDescription.Compact.ToEnumerable())
    {
    }
 
    public IEnumerable<string> Assertions { get; } = assertions;
    
    public IEnumerable<Explanation> Underlying { get; internal set; } = Enumerable.Empty<Explanation>();
    
    public override string ToString() => string.Join(", ", Assertions);

    private string GetDebuggerDisplay() => HaveComprehensiveAssertions() || !Underlying.Any()
        ? ToString()
        : $$"""
            {{ToString()}} { {{string.Join(", ", Underlying.GetAssertions())}} }
            """;

    private bool HaveComprehensiveAssertions() => Assertions.HasAtLeast(2);
}