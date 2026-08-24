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
    internal Explanation(string assertion, IEnumerable<BooleanResultBase>? causes = null, IEnumerable<BooleanResultBase>? results = null)
    {
        string[] assertions = [assertion];

        Causes = causes ?? [];
        Results = results ?? [];
        Assertions = assertions;
        AllAssertions = assertions;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class that redefines assertions.
    /// </summary>
    /// <param name="assertions">The assertions.</param>
    /// <param name="causes">The causes.</param>
    /// <param name="results">The results that took part in the evaluation.</param>
    internal Explanation(IEnumerable<string> assertions, IEnumerable<BooleanResultBase>? causes = null, IEnumerable<BooleanResultBase>? results = null)
    {
        var distinctAssertions = assertions.ToArray();

        Causes = causes ?? [];
        Results = results ?? [];
        Assertions = distinctAssertions;
        AllAssertions = distinctAssertions;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class that redefines assertions.
    /// </summary>
    /// <param name="assertions">The assertions.</param>
    /// <param name="cause">The cause.</param>
    internal Explanation(IEnumerable<string> assertions, BooleanResultBase cause)
    {
        var distinctAssertions = assertions.ToArray();

        BooleanResultBase[] causes = [cause];
        Causes = causes;
        Results = causes;
        Assertions = distinctAssertions;
        AllAssertions = distinctAssertions;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class, which co-opts the assertions from the causes.
    /// </summary>
    /// <param name="causes">The causes.</param>
    /// <param name="results">The results that took part in the evaluation.</param>
    internal Explanation(IEnumerable<BooleanResultBase>? causes = null, IEnumerable<BooleanResultBase>? results = null)
    {
        var causeCollection = causes as ICollection<BooleanResultBase> ?? causes?.ToArray() ?? [];
        var assertions = causeCollection.GetAssertions();

        var allResult = results as ICollection<BooleanResultBase> ?? results?.ToArray() ?? [];
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
    public IEnumerable<Explanation> Underlying =>
        _underlying ??= PostOrderFold.Fold(this, DescendCausal, CombineCausal, ReadUnderlying, WriteUnderlying);

    /// <summary>
    /// Gets the all underlying explanations, regardless of whether they determined the outcome.
    /// </summary>
    public IEnumerable<Explanation> AllUnderlying =>
        _allUnderlying ??= PostOrderFold.Fold(this, DescendAll, CombineAll, ReadAllUnderlying, WriteAllUnderlying);

    private Explanation[]? _underlying;

    private Explanation[]? _allUnderlying;

    private Resolution<Explanation> CausalResolution =>
        field ??= Resolve(
            Assertions,
            Causes,
            cause => cause.UnderlyingAssertionSources,
            explanation => explanation.Assertions);

    private Resolution<Explanation> AllResolution =>
        field ??= Resolve(
            Assertions,
            Results,
            result => result.UnderlyingAllAssertionSources,
            explanation => explanation.AllAssertions);

    private static readonly Func<Explanation, IReadOnlyList<Explanation>> DescendCausal =
        explanation => explanation.CausalResolution.Collapse ? explanation.CausalResolution.Children : [];

    private static readonly Func<Explanation, IReadOnlyList<Explanation>> DescendAll =
        explanation => explanation.AllResolution.Collapse ? explanation.AllResolution.Children : [];

    private static readonly Func<Explanation, IReadOnlyList<Explanation[]>, Explanation[]> CombineCausal =
        (explanation, folded) => explanation.CausalResolution.Collapse
            ? folded.Flatten()
            : explanation.CausalResolution.Children;

    private static readonly Func<Explanation, IReadOnlyList<Explanation[]>, Explanation[]> CombineAll =
        (explanation, folded) => explanation.AllResolution.Collapse
            ? folded.Flatten()
            : explanation.AllResolution.Children;

    private static readonly Func<Explanation, Explanation[]?> ReadUnderlying =
        explanation => explanation._underlying;

    private static readonly Action<Explanation, Explanation[]> WriteUnderlying =
        (explanation, underlying) => explanation._underlying = underlying;

    private static readonly Func<Explanation, Explanation[]?> ReadAllUnderlying =
        explanation => explanation._allUnderlying;

    private static readonly Action<Explanation, Explanation[]> WriteAllUnderlying =
        (explanation, underlying) => explanation._allUnderlying = underlying;

    private string? _toString;

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => _toString ??= Assertions.Serialize();

    private static Resolution<Explanation> Resolve(
        IEnumerable<string> assertions,
        IEnumerable<BooleanResultBase> from,
        Func<BooleanResultBase, IEnumerable<BooleanResultBase>> sourcesOf,
        Func<Explanation, IEnumerable<string>> assertionsOf)
    {
        var children = from
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult => sourcesOf(result),
                    _ => result.ToEnumerable()
                })
            .Select(result => result.Explanation)
            .ToArray();

        var childAssertions = children
            .SelectMany(assertionsOf)
            .DistinctWithOrderPreserved()
            .ToArray();

        return new Resolution<Explanation>(children, childAssertions.SequenceEqual(assertions));
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
        var hasComprehensiveAssertions = Assertions.HasAtLeast(2);
        return hasComprehensiveAssertions || !Underlying.Any()
            ? ToString()
            : $$"""{{ToString()}} { {{Underlying.GetAssertions().Serialize()}} }""";
    }
}
