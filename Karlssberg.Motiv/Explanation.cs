using Humanizer;

namespace Karlssberg.Motiv;

public sealed record Explanation(IEnumerable<string> Reasons)
{
    public Explanation(string reason) : this([reason])
    {
    }

    public IEnumerable<string> Reasons { get; } = Reasons.Distinct().OrderBy(d => d);
    
    public IEnumerable<Explanation> Underlying { get; internal set; } = Enumerable.Empty<Explanation>();

    public IEnumerable<string> DeepReasons => FindDeepReasons(Underlying)
        .SelectMany(e => e.Reasons)
        .Distinct();

    public override string ToString() => Reasons.Humanize();
    
    private static IEnumerable<Explanation> FindDeepReasons(IEnumerable<Explanation> explanations)
    {
        foreach (var explanation in explanations)
        {
            var underlyingExplanations = false;
            foreach (var deepExplanation in FindDeepReasons(explanation.Underlying))
            {
                underlyingExplanations = true;
                yield return deepExplanation;
            }
            
            if (!underlyingExplanations)
            {
                yield return explanation;
            }
        }
            
    }
}