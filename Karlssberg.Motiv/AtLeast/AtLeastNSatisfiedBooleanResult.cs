namespace Karlssberg.Motiv.AtLeast;

/// <summary>
/// Represents a boolean result that is satisfied if at least a specified number of operand results are satisfied.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtLeastNSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>, IAtLeastNSatisfiedBooleanResult<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AtLeastNSatisfiedBooleanResult{TMetadata}"/> class.
    /// </summary>
    /// <param name="minimum">The minimum number of operand results that need to be satisfied.</param>
    /// <param name="metadataFactory">A function that generates metadata based on the overall satisfaction of the result.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AtLeastNSatisfiedBooleanResult(
        int minimum,
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = UnderlyingResults.Count(result => result.IsSatisfied) >= minimum;

        Minimum = minimum;
        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    /// <summary>
    /// Gets the substitute metadata associated with the boolean result.
    /// </summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    /// <summary>
    /// Gets the collection of operand results.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>
    /// Gets the collection of determinative operand results that have the same satisfaction status as the overall result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeResults => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <summary>
    /// Gets the minimum number of operand results that need to be satisfied.
    /// </summary>
    public int Minimum { get; }

    /// <summary>
    /// Gets a value indicating whether the boolean result is satisfied.
    /// </summary>
    public override bool IsSatisfied { get; }

    /// <summary>
    /// Gets the description of the boolean result.
    /// </summary>
    public override string Description => $"AT_LEAST_{Minimum}:{IsSatisfiedDisplayText}({string.Join(", ", UnderlyingResults)})";

    /// <summary>
    /// Gets the reasons associated with the boolean result.
    /// </summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeResults.SelectMany(r => r.GatherReasons());
}