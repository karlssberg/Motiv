using System.Diagnostics;
using Humanizer;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{ToString()}")]
public sealed class Explanation(IEnumerable<string> assertions)
{
    public Explanation(ResultDescriptionBase resultDescription) : this(resultDescription.Compact.ToEnumerable())
    {
    }
 
    public IEnumerable<string> Assertions { get; } = assertions;
    
    public IEnumerable<Explanation> Underlying { get; internal set; } = Enumerable.Empty<Explanation>();

    public IEnumerable<string> DeepAssertions =>
        FindUnderlyingReasons(Underlying)
            .SelectMany(e => e.Assertions)
            .Distinct();

    public override string ToString() => Assertions.Humanize();

    private static IEnumerable<Explanation> FindUnderlyingReasons(IEnumerable<Explanation> reasons)
    {
        foreach (var reason in reasons)
        {
            var underlyingExplanations = false;
            foreach (var deepExplanation in FindUnderlyingReasons(reason.Underlying))
            {
                underlyingExplanations = true;
                yield return deepExplanation;
            }
            
            if (!underlyingExplanations)
            {
                yield return reason;
            }
        }
    }
}