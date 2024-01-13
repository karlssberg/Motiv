namespace Karlssberg.Motiv.Any;

/// <summary>Represents a boolean result that indicates whether any of the operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The type of the model used to evaluate each underlying operand. </typeparam>
public sealed class AnySatisfiedBooleanResult<TModel, TMetadata> : BooleanResultBase<TMetadata>, IAnySatisfiedBooleanResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AnySatisfiedBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="metadataFactory">A function that creates metadata based on the boolean result.</param>
    /// <param name="operandResults">The operand results to evaluate.</param>
    internal AnySatisfiedBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultWithModel<TModel, TMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = UnderlyingResults.Any(result => result.IsSatisfied);

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    /// <summary>Gets the operand results used to evaluate the boolean result.</summary>
    public IEnumerable<BooleanResultWithModel<TModel, TMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    public IEnumerable<BooleanResultWithModel<TModel, TMetadata>> DeterminativeOperandResults => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.UnderlyingResults => UnderlyingResults;

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.DeterminativeResults => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <inheritdoc />
    public override bool IsSatisfied { get; }

    /// <inheritdoc />
    public override string Description => $"ANY:{IsSatisfiedDisplayText}({string.Join(", ", UnderlyingResults)})";

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperandResults.SelectMany(r => r.GatherReasons());
}