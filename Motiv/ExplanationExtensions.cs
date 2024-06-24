namespace Motiv;

/// <summary>
/// Provides extension methods for explanations.
/// </summary>
public static class ExplanationExtensions
{
    /// <summary>
    /// Gets the assertions from a collection of explanations.
    /// </summary>
    /// <param name="explanations">The explanations.</param>
    /// <returns>The aggregation of the assertions contained within the supplied explanations.</returns>
    public static IEnumerable<string> GetAssertions(
        this IEnumerable<Explanation> explanations) =>
        explanations.SelectMany(e => e.Assertions).DistinctWithOrderPreserved();

    internal static Explanation CreateExplanation(
        this IEnumerable<BooleanResultBase> causes)
    {
        var causeCollection = causes as ICollection<BooleanResultBase> ?? causes.ToArray();
        var assertions = causeCollection.GetAssertions();

        return new Explanation(assertions, causeCollection);
    }
}