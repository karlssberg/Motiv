using Humanizer;

namespace Karlssberg.Motiv.All;

/// <summary>Represents a boolean result that indicates whether all operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The model used to evaluate each underlying operand.</typeparam>
/// <typeparam name="TMetadata">The type of metadata associated with each underlying operand.</typeparam>
public sealed class AllSatisfiedBooleanResult<TModel, TMetadata> : 
    BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>, IHigherOrderLogicalOperatorResult<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AllSatisfiedBooleanResult{TModel, TMetadata, TMetadata}" /> class.
    /// </summary>
    /// <param name="isSatisfied"></param>
    /// <param name="metadata">A function that creates metadata based on the overall satisfaction of the operand results.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AllSatisfiedBooleanResult(
        bool isSatisfied,
        IEnumerable<BooleanResultWithModel<TModel, TMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        IsSatisfied = isSatisfied;
    }

    /// <summary>Gets the collection of operand results.</summary>
    /// +
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction as the overall result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);


    /// <inheritdoc cref="BooleanResultBase{TMetadata}.IsSatisfied" />
    public override bool IsSatisfied { get; }

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Description" />
    public override string Description
    {
        get
        {
            var satisfiedCount = UnderlyingResults.Count(result => result.IsSatisfied);
            var higherOrderStatement = $"ALL{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
            return $"{higherOrderStatement}({DeterminativeOperands.Count()}x {Reasons.Humanize()})";
        }
    }

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}