namespace Karlssberg.Motiv.Any;

/// <summary>
/// Represents a boolean result that indicates whether any of the operand results are satisfied.
/// </summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
public sealed class AnySatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnySatisfiedBooleanResult{TMetadata}"/> class.
    /// </summary>
    /// <param name="metadataFactory">A function that creates metadata based on the boolean result.</param>
    /// <param name="operandResults">The operand results to evaluate.</param>
    internal AnySatisfiedBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        OperandResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = OperandResults.Any(result => result.IsSatisfied);

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    /// <summary>
    /// Gets the substitute metadata associated with the boolean result.
    /// </summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    /// <summary>
    /// Gets the operand results used to evaluate the boolean result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }

    /// <summary>
    /// Gets the determinative operand results that have the same satisfaction status as the boolean result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <inheritdoc />
    public override bool IsSatisfied { get; }

    /// <inheritdoc />
    public override string Description => $"ANY:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";

    /// <inheritdoc />
    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}