using Humanizer;

namespace Karlssberg.Motiv.All;

/// <summary>Represents a boolean result that indicates whether all operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The model used to evaluate each underlying operand.</typeparam>
/// <typeparam name="TMetadata">The type of metadata associated with each underlying operand.</typeparam>
internal sealed class AllBooleanResult<TModel, TMetadata> : 
    BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>, IHigherOrderLogicalOperatorResult<TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AllBooleanResult{TModel,TMetadata}" /> class.
    /// </summary>
    /// <param name="isSatisfied"></param>
    /// <param name="metadata">A function that creates metadata based on the overall satisfaction of the operand results.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AllBooleanResult(
        bool isSatisfied,
        IEnumerable<BooleanResultWithModel<TModel, TMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        Value = isSatisfied;
    }

    /// <summary>Gets the collection of operand results.</summary>
    /// +
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction as the overall result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(result => result.Value == Value);


    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Value" />
    public override bool Value { get; }

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Description" />
    public override string Description
    {
        get
        {
            var satisfiedCount = UnderlyingResults.Count(result => result.Value);
            var higherOrderStatement = $"ALL{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
            return $"{higherOrderStatement}({DeterminativeOperands.Count()}x {Reasons.Humanize()})";
        }
    }

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}