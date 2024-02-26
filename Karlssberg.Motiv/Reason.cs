using System.Diagnostics;
using Humanizer;

namespace Karlssberg.Motiv;

[DebuggerDisplay("{ToString()}")]
public sealed class Reason(IEnumerable<string> assertions)
{
    public Reason(IAssertion assertion) : this(assertion.Short.ToEnumerable())
    {
    }
 
    public IEnumerable<string> Assertions { get; } = assertions;
    
    public IEnumerable<Reason> Underlying { get; internal set; } = Enumerable.Empty<Reason>();

    public IEnumerable<string> DeepAssertions =>
        FindUnderlyingReasons(Underlying)
            .SelectMany(e => e.Assertions)
            .Distinct();

    public override string ToString() => Assertions.Humanize();

    private static IEnumerable<Reason> FindUnderlyingReasons(IEnumerable<Reason> reasons)
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