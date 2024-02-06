using Humanizer;

namespace Karlssberg.Motiv.AtLeast;

/// <summary>Represents a boolean result that is satisfied if at least a specified number of operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtLeastBooleanResult<TMetadata> : 
    BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AtLeastBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="isSatisfied"></param>
    /// <param name="minimum">The minimum number of operand results that need to be satisfied (inclusive).</param>
    /// <param name="metadata">A function that generates metadata based on the overall satisfaction of the result.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AtLeastBooleanResult(
        bool isSatisfied,
        int minimum,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        Minimum = minimum.ThrowIfLessThan(0, nameof(minimum));
        
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        Value = isSatisfied;
    }

    /// <summary>Gets the minimum number of operand results that need to be satisfied.</summary>
    public int Minimum { get; }

    /// <summary>Gets the collection of operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>
    ///     Gets the collection of determinative operand results that have the same satisfaction status as the overall
    ///     result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => Value switch
    {
        true => UnderlyingResults.Where(result => result.Value == Value),
        false => UnderlyingResults
    };

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Value { get; }

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description => GetDescription();

    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Value);
        var higherOrderStatement =
            $"AT_LEAST_{Minimum}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
                
        return DeterminativeOperands.Any()
            ? $"{higherOrderStatement}({DeterminativeOperands.Count()}x {Reasons.Humanize()})"
            : higherOrderStatement;
    }

    /// <summary>Gets the reasons associated with the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(r => r.GatherReasons());
}