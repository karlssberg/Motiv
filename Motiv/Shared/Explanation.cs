using System.Diagnostics;
using Motiv.Traversal;

namespace Motiv.Shared;

/// <summary>
/// Represents an explanation for a boolean result, whilst also encapsulating underlying explanations (if any).
/// </summary>
[DebuggerDisplay("{Debug}")]
public sealed class Explanation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class that redefines assertions.
    /// </summary>
    /// <param name="assertion">The assertion.</param>
    /// <param name="causes">The causes.</param>
    /// <param name="results">The results that took part in the evaluation.</param>
    internal Explanation(string assertion, IEnumerable<BooleanResultBase> causes, IEnumerable<BooleanResultBase> results)
        : this(assertion.ToEnumerable(), causes, results)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class that redefines assertions.
    /// </summary>
    /// <param name="assertions">The assertions.</param>
    /// <param name="causes">The causes.</param>
    /// <param name="results">The results that took part in the evaluation.</param>
    internal Explanation(IEnumerable<string> assertions, IEnumerable<BooleanResultBase> causes, IEnumerable<BooleanResultBase> results)
    {
        var distinctAssertions = assertions.DistinctWithOrderPreserved().ToArray();

        Causes = causes;
        Results = results;
        Assertions = distinctAssertions;
        AllAssertions = distinctAssertions;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class, which co-opts the assertions from the causes.
    /// </summary>
    /// <param name="causes">The causes.</param>
    /// <param name="results">The results that took part in the evaluation.</param>
    internal Explanation(IEnumerable<BooleanResultBase> causes, IEnumerable<BooleanResultBase> results)
    {
        var causeCollection = causes as ICollection<BooleanResultBase> ?? causes.ToArray();
        var assertions = causeCollection.GetAssertions();

        var allResult = results as ICollection<BooleanResultBase> ?? results.ToArray();
        var allAssertions = allResult.GetAllAssertions();

        Assertions = assertions;
        AllAssertions = allAssertions;
        Causes = causeCollection;
        Results = allResult;
    }

    /// <summary>
    /// Gets the causes.
    /// </summary>
    public IEnumerable<BooleanResultBase> Causes { get; }

    /// <summary>
    /// Gets the causes.
    /// </summary>
    public IEnumerable<BooleanResultBase> Results { get; }

    /// <summary>
    /// Gets the assertions yielded from results that determined the outcome.
    /// </summary>
    public IEnumerable<string> Assertions { get; }

    /// <summary>
    /// Gets the assertions yielded from all results that took part in the evaluation.
    /// </summary>
    public IEnumerable<string> AllAssertions { get; }

    /// <summary>
    /// Gets the underlying explanations of the causes.
    /// </summary>
    public IEnumerable<Explanation> Underlying => ResolveUnderlying(Assertions, Causes);

    /// <summary>
    /// Gets the all underlying explanations, regardless of whether they determined the outcome.
    /// </summary>
    public IEnumerable<Explanation> AllUnderlying => ResolveAllUnderlying(Assertions, Results);

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => Assertions.Serialize();

    private static IEnumerable<Explanation> ResolveUnderlying(
        IEnumerable<string> assertions,
        IEnumerable<BooleanResultBase> causes)
    {
        var underlying = causes
            .SelectMany(cause =>
                cause switch
                {
                    IBooleanOperationResult => cause.UnderlyingAssertionSources,
                    _ => cause.ToEnumerable()
                })
            .Select(cause => cause.Explanation)
            .ToArray();

        var underlyingAssertions = underlying
            .SelectMany(explanation => explanation.Assertions)
            .DistinctWithOrderPreserved();

        var doesParentEqualChildAssertion = underlyingAssertions.SequenceEqual(assertions);

        return doesParentEqualChildAssertion
            ? underlying.SelectMany(result => result.Underlying)
            : underlying;
    }

    private static IEnumerable<Explanation> ResolveAllUnderlying(
        IEnumerable<string> assertions,
        IEnumerable<BooleanResultBase> results)
    {
        var allUnderlying = results
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult => result.UnderlyingAllAssertionSources,
                    _ => result.ToEnumerable()
                })
            .Select(cause => cause.Explanation)
            .ToArray();

        var allUnderlyingAssertions = allUnderlying
            .SelectMany(explanation => explanation.AllAssertions)
            .DistinctWithOrderPreserved();

        var doesParentEqualChildAssertion = allUnderlyingAssertions.SequenceEqual(assertions);

        return doesParentEqualChildAssertion
            ? allUnderlying.SelectMany(result => result.AllUnderlying)
            : allUnderlying;
    }

    /// <summary>
    /// Gets the debug display string.
    /// </summary>
    private string Debug => GetDebuggerDisplay();

    /// <summary>
    /// Gets the debugger display string.
    /// </summary>
    /// <returns>The debugger display string.</returns>
    private string GetDebuggerDisplay()
    {
        return HaveComprehensiveAssertions() || !Underlying.Any()
            ? ToString()
            : $$"""{{ToString()}} { {{Underlying.GetAssertions().Serialize()}} }""";

        bool HaveComprehensiveAssertions() => Assertions.HasAtLeast(2);
    }
}
