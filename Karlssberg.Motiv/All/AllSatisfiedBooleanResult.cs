using Karlssberg.Motiv;

/// <summary>
/// Represents a boolean result that indicates whether all operand results are satisfied.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AllSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AllSatisfiedBooleanResult{TMetadata}"/> class.
    /// </summary>
    /// <param name="metadataFactory">A function that creates metadata based on the overall satisfaction of the operand results.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AllSatisfiedBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        OperandResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = OperandResults.All(result => result.IsSatisfied);

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
    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }

    /// <summary>
    /// Gets the determinative operand results that have the same satisfaction as the overall result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <inheritdoc />
    public override bool IsSatisfied { get; }

    /// <inheritdoc />
    public override string Description => $"ALL:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";

    /// <inheritdoc />
    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}