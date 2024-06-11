using System.Diagnostics;

namespace Motiv;

/// <summary>
/// Represents an explanation for a boolean result, whilst also encapsulating underlying explanations (if any).
/// </summary>
[DebuggerDisplay("{Debug}")]
public sealed class Explanation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class.
    /// </summary>
    /// <param name="assertion">The assertion.</param>
    /// <param name="causes">The causes.</param>
    internal Explanation(string assertion, IEnumerable<BooleanResultBase> causes)
        : this(assertion.ToEnumerable(), causes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Explanation"/> class.
    /// </summary>
    /// <param name="assertions">The assertions.</param>
    /// <param name="causes">The causes.</param>
    internal Explanation(IEnumerable<string> assertions, IEnumerable<BooleanResultBase> causes)
    {
        var assertionsArray = assertions as string[] ?? assertions.ToArray();
        Causes = causes;
        Assertions = assertionsArray.DistinctWithOrderPreserved();
    }

    /// <summary>
    /// Gets the causes.
    /// </summary>
    public IEnumerable<BooleanResultBase> Causes { get; }

    /// <summary>
    /// Gets the assertions.
    /// </summary>
    public IEnumerable<string> Assertions { get; }

    /// <summary>
    /// Gets the underlying explanations.
    /// </summary>
    public IEnumerable<Explanation> Underlying => ResolveUnderlying(Assertions, Causes);

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => Assertions.Serialize();

    /// <summary>
    /// Resolves the underlying explanations.
    /// </summary>
    /// <param name="assertions">The assertions.</param>
    /// <param name="causes">The causes.</param>
    /// <returns>The underlying explanations.</returns>
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