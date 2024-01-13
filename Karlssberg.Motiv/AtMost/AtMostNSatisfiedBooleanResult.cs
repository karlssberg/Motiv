namespace Karlssberg.Motiv.AtMost;

/// <summary>
/// Represents the result of an "at most" boolean operation with a maximum number of satisfied operands.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtMostNSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>, IAtMostNSatisfiedBooleanResult<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AtMostNSatisfiedBooleanResult{TMetadata}"/> class.
    /// </summary>
    /// <param name="maximum">The maximum number of satisfied operands allowed.</param>
    /// <param name="metadataFactory">A function that creates metadata based on the overall satisfaction of the operands.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AtMostNSatisfiedBooleanResult(
        int maximum,
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = UnderlyingResults.Count(result => result.IsSatisfied) <= maximum;

        Maximum = maximum;
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
    /// Gets the maximum number of satisfied operands allowed.
    /// </summary>
    public int Maximum { get; }

    /// <summary>
    /// Gets a value indicating whether the boolean result is satisfied.
    /// </summary>
    public override bool IsSatisfied { get; }

    /// <summary>
    /// Gets the description of the boolean result.
    /// </summary>
    public override string Description => $"AT_MOST_{Maximum}:{IsSatisfiedDisplayText}({string.Join(", ", UnderlyingResults)})";

    /// <summary>
    /// Gets the reasons for the boolean result.
    /// </summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeResults.SelectMany(r => r.GatherReasons());
}