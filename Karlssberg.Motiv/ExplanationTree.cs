﻿using System.Diagnostics;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{GetDebuggerDisplay()}")]
public sealed class ExplanationTree(IEnumerable<string> assertions)
{
    private readonly Lazy<IEnumerable<string>> _lazyDistinctAssertions = new (assertions.Distinct);
    
    public ExplanationTree(string assertion) 
        : this(assertion.ToEnumerable())
    {
    }
    
    public ExplanationTree(ResultDescriptionBase resultDescription) 
        : this(resultDescription.Reason.ToEnumerable())
    {
    }
 
    public IEnumerable<string> Assertions => _lazyDistinctAssertions.Value;
    
    public IEnumerable<ExplanationTree> Underlying { get; internal set; } = Enumerable.Empty<ExplanationTree>();
    
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